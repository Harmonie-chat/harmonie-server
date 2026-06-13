using System.ComponentModel.DataAnnotations;

namespace Harmonie.Application.Services.Notifications;

public sealed class PushNotificationOptions
{
    public const string SectionName = "PushNotifications";

    public bool Enabled { get; set; }

    [Range(1, 500)]
    public int BatchSize { get; set; } = 25;

    [Range(1, 3600)]
    public int PollIntervalSeconds { get; set; } = 5;

    [Range(1, 3600)]
    public int LockDurationSeconds { get; set; } = 300;

    [Range(1, 20)]
    public int MaxAttempts { get; set; } = 5;

    [Range(1, 3600)]
    public int RetryBaseDelaySeconds { get; set; } = 30;
}
