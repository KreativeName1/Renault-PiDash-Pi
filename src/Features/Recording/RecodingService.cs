using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using PiDash.Core;

namespace PiDash.Features.Recording;

public sealed class RecordingService : BackgroundService
{
    private readonly ILogger<RecordingService> _log;
    private readonly IConfiguration _cfg;
    private readonly TelemetryState _telemetry;
    private readonly IClock _clock;

    public RecordingService(ILogger<RecordingService> log, IConfiguration cfg, TelemetryState telemetry, IClock clock)
    {
        _log = log;
        _cfg = cfg;
        _telemetry = telemetry;
        _clock = clock;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabledOnBoot = _cfg.GetValue("Recording:EnabledOnBoot", false);
        if (!enabledOnBoot)
        {
            _log.LogInformation("RecordingService idle (EnabledOnBoot=false).");
            // Stay alive but do nothing. Later you'll toggle recording via UI/buttons.
            await Task.Delay(Timeout.Infinite, stoppingToken);
            return;
        }

        var dbPath = _cfg.GetValue<string>("Recording:DatabasePath") ?? "/var/lib/pidash/pidash.sqlite";
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath) ?? ".");

        var schemaPath = Path.Combine(AppContext.BaseDirectory, "Features/Recording/Schema.sql");
        if (!File.Exists(schemaPath))
        {
            // fallback if copied differently during publish
            schemaPath = Path.Combine(AppContext.BaseDirectory, "Schema.sql");
        }

        using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync(stoppingToken);

        // Pragmas
        await Exec(conn, "PRAGMA journal_mode=WAL;", stoppingToken);
        await Exec(conn, "PRAGMA synchronous=NORMAL;", stoppingToken);

        // Schema
        var schema = File.ReadAllText(schemaPath);
        await Exec(conn, schema, stoppingToken);

        // Start session
        long startTs = _clock.UtcUnixMilliseconds;
        long sessionId = await InsertSession(conn, startTs, stoppingToken);

        _log.LogInformation("Recording enabled. DB={DbPath}, Session={SessionId}", dbPath, sessionId);
    
        // Prepared insert - matches SensorSample field order
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO samples(
                session_id, ts,
                oil_kpa, oiltemp_x10, vbat_mv,
                gps_long_x10_6, gps_lat_x10_6, gps_speed_x10,
                accelX_mg, accelY_mg, accelZ_mg,
                gyroX_x10, gyroY_x10, gyroZ_x10,
                roll_x10, pitch_x10, yaw_x10, light_lux)
            VALUES (
                $sid, $ts,
                $oil, $oilT, $vbat,
                $gpsLong, $gpsLat, $gpsSpeed,
                $accelX, $accelY, $accelZ,
                $gyroX, $gyroY, $gyroZ,
                $roll, $pitch, $yaw, $light)
";
        cmd.Parameters.Add("$sid", SqliteType.Integer);
        cmd.Parameters.Add("$ts", SqliteType.Integer);
        cmd.Parameters.Add("$oil", SqliteType.Integer);
        cmd.Parameters.Add("$oilT", SqliteType.Integer);
        cmd.Parameters.Add("$vbat", SqliteType.Integer);
        cmd.Parameters.Add("$gpsLong", SqliteType.Integer);
        cmd.Parameters.Add("$gpsLat", SqliteType.Integer);
        cmd.Parameters.Add("$gpsSpeed", SqliteType.Integer);
        cmd.Parameters.Add("$accelX", SqliteType.Integer);
        cmd.Parameters.Add("$accelY", SqliteType.Integer);
        cmd.Parameters.Add("$accelZ", SqliteType.Integer);
        cmd.Parameters.Add("$gyroX", SqliteType.Integer);
        cmd.Parameters.Add("$gyroY", SqliteType.Integer);
        cmd.Parameters.Add("$gyroZ", SqliteType.Integer);
        cmd.Parameters.Add("$roll", SqliteType.Integer);
        cmd.Parameters.Add("$pitch", SqliteType.Integer);
        cmd.Parameters.Add("$yaw", SqliteType.Integer);
        cmd.Parameters.Add("$light", SqliteType.Integer);

        int batchRows = _cfg.GetValue("Recording:BatchRows", 200);
        var lastCommitMs = _clock.MonotonicMilliseconds;
        int inBatch = 0;

        var tx = conn.BeginTransaction();
        cmd.Transaction = tx;

        var nextTick = _clock.MonotonicMilliseconds;
        const long sampleIntervalMs = 20; // 50 Hz

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                nextTick += sampleIntervalMs;

                long ts = _clock.UtcUnixMilliseconds;
                
                // Get atomic snapshot of all sensor values
                var sample = _telemetry.ToSample();

                cmd.Parameters["$sid"].Value = sessionId;
                cmd.Parameters["$ts"].Value = ts;
                
                // Map sample fields to parameters (matches SensorSample field order)
                cmd.Parameters["$oil"].Value = sample.OilPressure_kPa;
                cmd.Parameters["$oilT"].Value = sample.OilTemp_C_x10;
                cmd.Parameters["$vbat"].Value = sample.Vbat_mV;
                cmd.Parameters["$gpsLong"].Value = sample.Gps_longitude_x10_6;
                cmd.Parameters["$gpsLat"].Value = sample.Gps_latitude_x10_6;
                cmd.Parameters["$gpsSpeed"].Value = sample.Gps_speed_kph_x10;
                cmd.Parameters["$accelX"].Value = sample.AccelX_mg;
                cmd.Parameters["$accelY"].Value = sample.AccelY_mg;
                cmd.Parameters["$accelZ"].Value = sample.AccelZ_mg;
                cmd.Parameters["$gyroX"].Value = sample.GyroX_dps_x10;
                cmd.Parameters["$gyroY"].Value = sample.GyroY_dps_x10;
                cmd.Parameters["$gyroZ"].Value = sample.GyroZ_dps_x10;
                cmd.Parameters["$roll"].Value = sample.Roll_deg_x10;
                cmd.Parameters["$pitch"].Value = sample.Pitch_deg_x10;
                cmd.Parameters["$yaw"].Value = sample.Yaw_deg_x10;
                cmd.Parameters["$light"].Value = sample.AmbientLight_lux;

                cmd.ExecuteNonQuery();
                inBatch++;

                var now = _clock.MonotonicMilliseconds;
                if (inBatch >= batchRows || (now - lastCommitMs) >= 1000)
                {
                    tx.Commit();
                    tx.Dispose();

                    // new transaction
                    inBatch = 0;
                    lastCommitMs = now;

                    tx = conn.BeginTransaction();
                    cmd.Transaction = tx;
                }

                try { await _clock.DelayUntilMs(nextTick, stoppingToken); }
                catch (OperationCanceledException) { }
            }
        }
        finally
        {
            tx?.Dispose();
        }

        // End session (best-effort)
        long endTs = _clock.UtcUnixMilliseconds;
        await Exec(conn, $"UPDATE sessions SET end_ts={endTs} WHERE id={sessionId};", CancellationToken.None);
    }

    private static async Task Exec(SqliteConnection conn, string sql, CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<long> InsertSession(SqliteConnection conn, long startTs, CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO sessions(start_ts) VALUES ($ts); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$ts", startTs);
        var result = await cmd.ExecuteScalarAsync(ct);
        return (long)(result ?? 0L);
    }
}
