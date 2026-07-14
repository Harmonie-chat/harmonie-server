namespace Harmonie.Application.Tests;

internal static class TestClock
{
    public static readonly DateTime UtcNow = new(2026, 7, 14, 12, 0, 0, DateTimeKind.Utc);

    public static TimeProvider Provider { get; } = new FixedTimeProvider(UtcNow);

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
