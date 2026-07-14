using System.ComponentModel.DataAnnotations;

namespace Harmonie.Application.Services.Notifications;

public sealed class NotificationCleanupOptions
{
    public const string SectionName = "NotificationCleanup";

    public bool Enabled { get; set; }

    [Range(1, 168)]
    public int PollIntervalHours { get; set; } = 24;

    [Range(1, 5_000)]
    public int BatchSize { get; set; } = 500;

    [Range(1, 3_650)]
    public int ProcessedOutboxRetentionDays { get; set; } = 7;

    [Range(1, 3_650)]
    public int FailedOutboxRetentionDays { get; set; } = 30;

    [Range(0, 3_650)]
    public int ExpiredDeviceRetentionDays { get; set; } = 7;
}
