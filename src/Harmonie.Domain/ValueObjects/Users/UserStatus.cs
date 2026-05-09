namespace Harmonie.Domain.ValueObjects.Users;

/// <summary>
/// User presence status value object.
/// Enforces validity at every entry point — construction, hydration, and update.
/// Only four statuses are valid: online, idle, dnd, invisible.
/// </summary>
public sealed record UserStatus
{
    private static readonly HashSet<string> ValidValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "online", "idle", "dnd", "invisible"
    };

    public string Value { get; }

    public static UserStatus Online { get; } = new("online");
    public static UserStatus Idle { get; } = new("idle");
    public static UserStatus Dnd { get; } = new("dnd");
    public static UserStatus Invisible { get; } = new("invisible");

    private UserStatus(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Create a UserStatus from a raw string.
    /// Validates against the known set of valid statuses.
    /// Case-insensitive; the stored value is always lowercase.
    /// </summary>
    public static Domain.Common.Result<UserStatus> Create(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Domain.Common.Result.Failure<UserStatus>("Status cannot be empty");

        if (!ValidValues.Contains(raw))
            return Domain.Common.Result.Failure<UserStatus>("Status must be one of: online, idle, dnd, invisible");

        return Domain.Common.Result.Success(new UserStatus(raw.ToLowerInvariant()));
    }

    public override string ToString() => Value;

    /// <summary>
    /// Implicit conversion to string for convenience in serialization and Dapper mapping.
    /// </summary>
    public static implicit operator string(UserStatus status) => status.Value;
}
