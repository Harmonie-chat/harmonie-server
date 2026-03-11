using Harmonie.Application.Interfaces;
using Harmonie.Domain.ValueObjects;

namespace Harmonie.Application.Common;

public static class MessageCursorCodec
{
    public static string Encode(MessageCursor cursor)
    {
        var utcCreatedAt = cursor.CreatedAtUtc.Kind == DateTimeKind.Utc
            ? cursor.CreatedAtUtc
            : DateTime.SpecifyKind(cursor.CreatedAtUtc, DateTimeKind.Utc);

        return $"{utcCreatedAt.Ticks}_{cursor.MessageId}";
    }

    public static bool TryParse(string? encodedCursor, out MessageCursor? cursor)
    {
        cursor = null;

        if (string.IsNullOrWhiteSpace(encodedCursor))
            return false;

        var separatorIndex = encodedCursor.IndexOf('_');
        if (separatorIndex <= 0 || separatorIndex >= encodedCursor.Length - 1)
            return false;

        var ticksPart = encodedCursor[..separatorIndex];
        var messageIdPart = encodedCursor[(separatorIndex + 1)..];

        if (!long.TryParse(ticksPart, out var ticks))
            return false;

        if (!MessageId.TryParse(messageIdPart, out var messageId) || messageId is null)
            return false;

        try
        {
            var createdAtUtc = new DateTime(ticks, DateTimeKind.Utc);
            cursor = new MessageCursor(createdAtUtc, messageId);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }
}
