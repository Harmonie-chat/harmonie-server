using Harmonie.Domain.Common;

namespace Harmonie.Domain.ValueObjects;

/// <summary>
/// Guild name value object with normalized validation.
/// </summary>
public sealed record GuildName
{
    public const int MinLength = 3;
    public const int MaxLength = 100;

    public string Value { get; }

    private GuildName(string value)
    {
        Value = value;
    }

    public static Result<GuildName> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result.Failure<GuildName>("Guild name is required");

        var normalized = value.Trim();

        if (normalized.Length < MinLength)
            return Result.Failure<GuildName>($"Guild name must be at least {MinLength} characters");

        if (normalized.Length > MaxLength)
            return Result.Failure<GuildName>($"Guild name cannot exceed {MaxLength} characters");

        return Result.Success(new GuildName(normalized));
    }

    public override string ToString() => Value;

    public static implicit operator string(GuildName guildName) => guildName.Value;
}
