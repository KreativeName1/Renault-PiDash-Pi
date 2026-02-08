using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using PiDash.Hardware.Spi;
using PiDash.Hardware.Spi.Protocol;
using System.Device.Spi;
using PiDash.Config;
using PiDash.Core;
using PiDash.Features.Sensors;

namespace PiDash.Features.Display;

public sealed class DisplayService : BackgroundService
{
    private readonly ILogger<DisplayService> _log;
    private readonly IConfiguration _cfg;
    private readonly DeviceMap _map;
    private readonly SpiDeviceFactory _spiFactory;
    private readonly TelemetryState _telemetry;
    private readonly IClock _clock;

    public DisplayService(ILogger<DisplayService> log, IConfiguration cfg, DeviceMap map, SpiDeviceFactory spiFactory, TelemetryState telemetry, IClock clock)
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
        var hz = _cfg.GetValue("Display:UpdateHz", 30);
        var intervalMs = 1000.0 / Math.Max(1, hz);

        // Check which displays are enabled
        var enable1 = _cfg.GetValue("Display:EnableDisplay1", true);
        var enable2 = _cfg.GetValue("Display:EnableDisplay2", false);
        var enable3 = _cfg.GetValue("Display:EnableDisplay3", false);

        // Create only enabled displays with error handling
        SpiLink? link1 = null, link2 = null, link3 = null;
        
        try
        {
            if (enable1)
            {
                try
                {
                    var spi1 = _spiFactory.Create(_map.Spi.Display1);
                    link1 = new SpiLink(spi1);
                    _log.LogInformation("Display1 initialized");
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Failed to initialize Display1, will continue without it");
                }
            }

            if (enable2)
            {
                try
                {
                    var spi2 = _spiFactory.Create(_map.Spi.Display2);
                    link2 = new SpiLink(spi2);
                    _log.LogInformation("Display2 initialized");
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Failed to initialize Display2, will continue without it");
                }
            }

            if (enable3)
            {
                try
                {
                    var spi3 = _spiFactory.Create(_map.Spi.Display3);
                    link3 = new SpiLink(spi3);
                    _log.LogInformation("Display3 initialized");
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Failed to initialize Display3, will continue without it");
                }
            }

            if (link1 == null && link2 == null && link3 == null)
            {
                _log.LogWarning("No displays available, DisplayService will idle");
                await Task.Delay(Timeout.Infinite, stoppingToken);
                return;
            }

            byte[] tx = new byte[128];
            byte[] rx = new byte[128];

            _log.LogInformation("DisplayService started at {Hz} Hz", hz);

            long lastSeq = -1;
            byte[] payload = new byte[SensorSampleExtensions.SENSOR_SAMPLE_BYTES];
            var nextTick = _clock.MonotonicMilliseconds;

            while (!stoppingToken.IsCancellationRequested)
            {
                nextTick += (long)intervalMs;

                var seq = _telemetry.Sequence;
                if (seq != lastSeq)
                {
                    lastSeq = seq;
                    
                    // Get snapshot and serialize to bytes
                    var sample = _telemetry.ToSample();
                    sample.ToBytes(payload);

                    if (link1 != null)
                        link1.SendAndReceive(MsgType.Dashboard, payload, tx, rx, out _, out _);
                    if (link2 != null)
                        link2.SendAndReceive(MsgType.Dashboard, payload, tx, rx, out _, out _);
                    if (link3 != null)
                        link3.SendAndReceive(MsgType.Dashboard, payload, tx, rx, out _, out _);
                }

                try { await _clock.DelayUntilMs(nextTick, stoppingToken); }
                catch (OperationCanceledException) { }
            }
        }
        finally
        {
            link1?.Dispose();
            link2?.Dispose();
            link3?.Dispose();
        }
    }
}
