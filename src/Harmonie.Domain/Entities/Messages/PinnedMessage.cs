using Harmonie.Domain.Common;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Domain.Entities.Messages;

public sealed class PinnedMessage
{
    public MessageId MessageId { get; }

    public UserId PinnedByUserId { get; }

    public DateTime PinnedAtUtc { get; }

    private PinnedMessage(
        MessageId messageId,
        UserId pinnedByUserId,
        DateTime pinnedAtUtc)
    {
        MessageId = messageId;
        PinnedByUserId = pinnedByUserId;
        PinnedAtUtc = pinnedAtUtc;
    }

    public static Result<PinnedMessage> Create(
        MessageId messageId,
        UserId pinnedByUserId)
    {
        if (messageId is null)
            return Result.Failure<PinnedMessage>("Message ID is required");

        if (pinnedByUserId is null)
            return Result.Failure<PinnedMessage>("User ID is required");

        return Result.Success(new PinnedMessage(
            messageId,
            pinnedByUserId,
            DateTime.UtcNow));
    }

    public static PinnedMessage Rehydrate(
        MessageId messageId,
        UserId pinnedByUserId,
        DateTime pinnedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(messageId);
        ArgumentNullException.ThrowIfNull(pinnedByUserId);

        return new PinnedMessage(messageId, pinnedByUserId, pinnedAtUtc);
    }
}
