using Harmonie.Application.Interfaces.Messages;

namespace Harmonie.Application.Common.Messages;

public static class ReactionUsersCursorCodec
{
    public static string Encode(ReactionUsersCursor cursor)
    {
        var utcCreatedAt = cursor.CreatedAtUtc.Kind == DateTimeKind.Utc
            ? cursor.CreatedAtUtc
            : DateTime.SpecifyKind(cursor.CreatedAtUtc, DateTimeKind.Utc);

        return $"{utcCreatedAt.Ticks}_{cursor.UserId}";
    }

    public static bool TryParse(string? encodedCursor, out ReactionUsersCursor? cursor)
    {
        cursor = null;

        if (string.IsNullOrWhiteSpace(encodedCursor))
            return false;

        var separatorIndex = encodedCursor.IndexOf('_');
        if (separatorIndex <= 0 || separatorIndex >= encodedCursor.Length - 1)
            return false;

        var ticksPart = encodedCursor[..separatorIndex];
        var userIdPart = encodedCursor[(separatorIndex + 1)..];

        if (!long.TryParse(ticksPart, out var ticks))
            return false;

        if (!Guid.TryParse(userIdPart, out var userId))
            return false;

        try
        {
            var createdAtUtc = new DateTime(ticks, DateTimeKind.Utc);
            cursor = new ReactionUsersCursor(createdAtUtc, userId);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }
}
