using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using PiDash.Hardware.Spi;
using PiDash.Hardware.Spi.Protocol;
using System.Device.Spi;
using PiDash.Config;
using PiDash.Core;

namespace PiDash.Features.Sensors;

public sealed class SensorService : BackgroundService
{
    private readonly ILogger<SensorService> _log;
    private readonly IConfiguration _cfg;
    private readonly DeviceMap _map;
    private readonly SpiDeviceFactory _spiFactory;
    private readonly TelemetryState _telemetry;
    private readonly IClock _clock;

    public SensorService(ILogger<SensorService> log, IConfiguration cfg, DeviceMap map, SpiDeviceFactory spiFactory, TelemetryState telemetry, IClock clock)
    {
        _log = log;
        _cfg = cfg;
        _map = map;
        _spiFactory = spiFactory;
        _telemetry = telemetry;
        _clock = clock;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var hz = _cfg.GetValue("Sensors:PollHz", 200);
        var intervalMs = 1000.0 / Math.Max(1, hz);

        const int maxRetryDelayMs = 30000; // 30 seconds max backoff
        int retryDelayMs = 1000;

        while (!stoppingToken.IsCancellationRequested)
        {
            SpiDevice? spi = null;
            SpiLink? link = null;

            try
            {
                spi = _spiFactory.Create(_map.Spi.SensorMcu);
                link = new SpiLink(spi);
                
                byte[] tx = new byte[128];
                byte[] rx = new byte[128];

                _log.LogInformation("SensorService started at {Hz} Hz", hz);
                retryDelayMs = 1000; // Reset backoff on successful connection

                var nextTick = _clock.MonotonicMilliseconds;

                while (!stoppingToken.IsCancellationRequested)
                {
                    nextTick += (long)intervalMs;

                    // Phase 1: request a sensor sample (empty payload)
                    if (link.SendAndReceive(MsgType.SensorSample, ReadOnlySpan<byte>.Empty, tx, rx, out var rt, out var rp)
                        && rt == MsgType.SensorSample
                        && rp.Length >= SensorSampleExtensions.SENSOR_SAMPLE_BYTES)
                    {
                        var sample = SensorSampleExtensions.FromBytes(rp);
                        _telemetry.ApplySensorSample(sample);
                    }

                    // Precise pacing with clock
                    try { await _clock.DelayUntilMs(nextTick, stoppingToken); }
                    catch (OperationCanceledException) { }
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _log.LogError(ex, "SensorService failed, will retry in {DelayMs}ms", retryDelayMs);
                
                link?.Dispose();
                spi?.Dispose();
                
                try { await Task.Delay(retryDelayMs, stoppingToken); }
                catch (OperationCanceledException) { break; }
                
                retryDelayMs = Math.Min(retryDelayMs * 2, maxRetryDelayMs);
            }
            finally
            {
                link?.Dispose();
                spi?.Dispose();
            }
        }
    }
}
