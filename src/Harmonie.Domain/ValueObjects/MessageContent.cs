using Harmonie.Domain.Common;

namespace Harmonie.Domain.ValueObjects;

/// <summary>
/// Message content normalized and validated before persistence.
/// </summary>
public sealed record MessageContent
{
    public const int MaxLength = 4000;

    public string Value { get; }

    private MessageContent(string value)
    {
        Value = value;
    }

    public static Result<MessageContent> Create(string? value)
    {
        if (value is null)
            return Result.Failure<MessageContent>("Message content is required");

        var normalized = value.Trim();
        if (normalized.Length == 0)
            return Result.Failure<MessageContent>("Message content cannot be empty");

        if (normalized.Length > MaxLength)
            return Result.Failure<MessageContent>($"Message content cannot exceed {MaxLength} characters");

        return Result.Success(new MessageContent(normalized));
    }

    public override string ToString() => Value;

    public static implicit operator string(MessageContent content) => content.Value;
}
