using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PiDash.Core.Diagnostics;

public sealed class HealthService : BackgroundService
{
    private readonly ILogger<HealthService> _log;
    private readonly IClock _clock;

    public HealthService(ILogger<HealthService> log, IClock clock)
    {
        _log = log;
        _clock = clock;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var path = "/tmp/pidash.health";
        _log.LogInformation("HealthService started ({Path})", path);

        var nextTick = _clock.MonotonicMilliseconds;
        const long intervalMs = 1000;

        while (!stoppingToken.IsCancellationRequested)
        {
            nextTick += intervalMs;

            try
            {
                var timestamp = _clock.UtcUnixMilliseconds;
                File.WriteAllText(path, timestamp.ToString());
            }
            catch { /* ignore */ }

            try { await _clock.DelayUntilMs(nextTick, stoppingToken); }
            catch (OperationCanceledException) { }
        }
    }
}