using Harmonie.Domain.Common;

namespace Harmonie.Domain.ValueObjects;

/// <summary>
/// Message content normalized and validated before persistence.
/// </summary>
public sealed record ChannelMessageContent
{
    public const int MaxLength = 4000;

    public string Value { get; }

    private ChannelMessageContent(string value)
    {
        Value = value;
    }

    public static Result<ChannelMessageContent> Create(string? value)
    {
        if (value is null)
            return Result.Failure<ChannelMessageContent>("Message content is required");

        var normalized = value.Trim();
        if (normalized.Length == 0)
            return Result.Failure<ChannelMessageContent>("Message content cannot be empty");

        if (normalized.Length > MaxLength)
            return Result.Failure<ChannelMessageContent>($"Message content cannot exceed {MaxLength} characters");

        return Result.Success(new ChannelMessageContent(normalized));
    }

    public override string ToString() => Value;

    public static implicit operator string(ChannelMessageContent content) => content.Value;
}
