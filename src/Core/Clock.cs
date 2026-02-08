using System.Diagnostics;

namespace PiDash.Core;

public interface IClock
{
    long MonotonicMilliseconds { get; }
    long UtcUnixMilliseconds { get; }
}

public sealed class SystemClock : IClock
{
    private static readonly double TickToMs = 1000.0 / Stopwatch.Frequency;

    public long MonotonicMilliseconds
        => (long)(Stopwatch.GetTimestamp() * TickToMs);

    public long UtcUnixMilliseconds
        => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}


public static class ClockExtensions
{
    public static async Task DelayUntilMs(this IClock clock, long targetMs, CancellationToken ct)
    {
        while (true)
        {
            var now = clock.MonotonicMilliseconds;
            var remaining = targetMs - now;
            if (remaining <= 0) return;

            // Sleep most of it, then re-check.
            var sleep = remaining > 5 ? remaining - 2 : 1;
            await Task.Delay((int)sleep, ct);
        }
    }
}