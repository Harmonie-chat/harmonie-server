using Harmonie.Domain.Common;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Domain.Entities.Conversations;

public sealed class ConversationParticipant
{
    public ConversationId ConversationId { get; }

    public UserId UserId { get; }

    public DateTime JoinedAtUtc { get; }

    public DateTime? HiddenAtUtc { get; private set; }

    private ConversationParticipant(
        ConversationId conversationId,
        UserId userId,
        DateTime joinedAtUtc,
        DateTime? hiddenAtUtc)
    {
        ConversationId = conversationId;
        UserId = userId;
        JoinedAtUtc = joinedAtUtc;
        HiddenAtUtc = hiddenAtUtc;
    }

    public static Result<ConversationParticipant> Create(
        ConversationId conversationId,
        UserId userId,
        DateTime joinedAtUtc)
    {
        if (conversationId is null)
            return Result.Failure<ConversationParticipant>("Conversation ID is required");

        if (userId is null)
            return Result.Failure<ConversationParticipant>("User ID is required");

        return Result.Success(new ConversationParticipant(
            conversationId,
            userId,
            joinedAtUtc,
            hiddenAtUtc: null));
    }

    public static ConversationParticipant Rehydrate(
        ConversationId conversationId,
        UserId userId,
        DateTime joinedAtUtc,
        DateTime? hiddenAtUtc)
    {
        ArgumentNullException.ThrowIfNull(conversationId);
        ArgumentNullException.ThrowIfNull(userId);

        return new ConversationParticipant(
            conversationId,
            userId,
            joinedAtUtc,
            hiddenAtUtc);
    }

    public void Hide(DateTime hiddenAtUtc)
    {
        HiddenAtUtc = hiddenAtUtc;
    }

    public void Unhide()
    {
        HiddenAtUtc = null;
    }
}
