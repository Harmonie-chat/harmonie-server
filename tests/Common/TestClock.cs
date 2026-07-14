using Microsoft.Extensions.Time.Testing;

namespace Harmonie.Testing;

internal static class TestClock
{
    private static readonly DateTimeOffset FixedUtcNow =
        new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);

    public static DateTime UtcNow => FixedUtcNow.UtcDateTime;

    public static FakeTimeProvider Create() => new(FixedUtcNow);
}
