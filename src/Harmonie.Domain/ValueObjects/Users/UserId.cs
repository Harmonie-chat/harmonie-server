using System.Diagnostics.CodeAnalysis;

namespace Harmonie.Domain.ValueObjects.Users;

/// <summary>
/// Strongly-typed identifier for User entities.
/// Prevents primitive obsession and provides type safety.
/// </summary>
public sealed record UserId : IParsable<UserId>
{
    public Guid Value { get; }

    private UserId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty", nameof(value));

        Value = value;
    }

    /// <summary>
    /// Create a new unique User ID
    /// </summary>
    public static UserId New() => new(Guid.NewGuid());

    /// <summary>
    /// Create a User ID from an existing GUID
    /// </summary>
    public static UserId From(Guid value) => new(value);

    /// <summary>
    /// Try to parse a string into a User ID
    /// </summary>
    public static bool TryParse(string value, out UserId? userId)
    {
        userId = null;
        if (!Guid.TryParse(value, out var guid) || guid == Guid.Empty)
            return false;

        userId = new UserId(guid);
        return true;
    }

    public static UserId Parse(string s, IFormatProvider? provider)
    {
        if (!TryParse(s, provider, out var result))
            throw new FormatException($"'{s}' is not a valid UserId.");
        return result;
    }

    public static bool TryParse(string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out UserId result)
    {
        result = null!; // Required by IParsable contract; guarded by [MaybeNullWhen(false)]
        if (string.IsNullOrWhiteSpace(s) || !Guid.TryParse(s, out var guid) || guid == Guid.Empty)
            return false;

        result = new UserId(guid);
        return true;
    }

    public override string ToString() => Value.ToString();

    // Implicit conversion to Guid for database operations
    public static implicit operator Guid(UserId userId) => userId.Value;
}
