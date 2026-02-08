using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using PiDash.Hardware.Spi;
using PiDash.Hardware.Spi.Protocol;
using System.Device.Spi;
using PiDash.Config;
using PiDash.Core;

namespace PiDash.Features.Can;

public sealed class CanService : BackgroundService
{
    private readonly ILogger<CanService> _log;
    private readonly IConfiguration _cfg;
    private readonly DeviceMap _map;
    private readonly SpiDeviceFactory _spiFactory;
    private readonly IClock _clock;

    public CanService(ILogger<CanService> log, IConfiguration cfg, DeviceMap map, SpiDeviceFactory spiFactory, IClock clock)
    {
        _log = log;
        _cfg = cfg;
        _map = map;
        _spiFactory = spiFactory;
        _clock = clock;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var hz = _cfg.GetValue("Can:PollHz", 200);
        var intervalMs = 1000.0 / Math.Max(1, hz);

        const int maxRetryDelayMs = 30000; // 30 seconds max backoff
        int retryDelayMs = 1000;

        while (!stoppingToken.IsCancellationRequested)
        {
            SpiDevice? spi = null;
            SpiLink? link = null;

            try
            {
                spi = _spiFactory.Create(_map.Spi.CanMcu);
                link = new SpiLink(spi);
                
                byte[] tx = new byte[128];
                byte[] rx = new byte[128];

                _log.LogInformation("CanService started at {Hz} Hz", hz);
                retryDelayMs = 1000; // Reset backoff on successful connection

                var nextTick = _clock.MonotonicMilliseconds;

                while (!stoppingToken.IsCancellationRequested)
                {
                    nextTick += (long)intervalMs;

                    // Phase 1: ask for a CAN status frame or mailbox read.
                    link.SendAndReceive(MsgType.CanFrame, ReadOnlySpan<byte>.Empty, tx, rx, out _, out _);

                    try { await _clock.DelayUntilMs(nextTick, stoppingToken); }
                    catch (OperationCanceledException) { }
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _log.LogError(ex, "CanService failed, will retry in {DelayMs}ms", retryDelayMs);
                
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