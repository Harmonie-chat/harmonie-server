using Harmonie.Application.Interfaces.Messages;

namespace Harmonie.Application.Common.Messages;

public static class PinnedMessagesCursorCodec
{
    public static string Encode(PinnedMessagesCursor cursor)
    {
        var utcPinnedAt = cursor.PinnedAtUtc.Kind == DateTimeKind.Utc
            ? cursor.PinnedAtUtc
            : DateTime.SpecifyKind(cursor.PinnedAtUtc, DateTimeKind.Utc);

        return $"{utcPinnedAt.Ticks}_{cursor.MessageId}";
    }

    public static bool TryParse(string? encodedCursor, out PinnedMessagesCursor? cursor)
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

        if (!Guid.TryParse(messageIdPart, out var messageId) || messageId == Guid.Empty)
            return false;

        try
        {
            var pinnedAtUtc = new DateTime(ticks, DateTimeKind.Utc);
            cursor = new PinnedMessagesCursor(pinnedAtUtc, messageId);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }
}
