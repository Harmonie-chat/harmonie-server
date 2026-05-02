using Harmonie.Application.Features.Conversations;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Interfaces.Conversations;

public interface IConversationNotifier
{
    Task NotifyConversationCreatedAsync(
        ConversationCreatedNotification notification,
        CancellationToken cancellationToken = default);

    Task NotifyParticipantLeftAsync(
        ConversationParticipantLeftNotification notification,
        CancellationToken cancellationToken = default);

    Task NotifyConversationUpdatedAsync(
        ConversationUpdatedNotification notification,
        CancellationToken cancellationToken = default);
}

public sealed record ConversationCreatedNotification(
    ConversationId ConversationId,
    string? Name,
    IReadOnlyList<ConversationParticipantDto> Participants);

public sealed record ConversationParticipantLeftNotification(
    ConversationId ConversationId,
    UserId UserId,
    string Username,
    string? DisplayName);

public sealed record ConversationUpdatedNotification(
    ConversationId ConversationId,
    string? Name);
