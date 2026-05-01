using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Domain.Entities.Conversations;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Conversations.DeleteConversation;

public sealed record DeleteConversationInput(ConversationId ConversationId);

public sealed class DeleteConversationHandler : IAuthenticatedHandler<DeleteConversationInput, bool>
{
    private static readonly TimeSpan NotificationTimeout = TimeSpan.FromSeconds(5);

    private readonly IConversationRepository _conversationRepository;
    private readonly IConversationParticipantRepository _participantRepository;
    private readonly IRealtimeGroupManager _realtimeGroupManager;
    private readonly IConversationNotifier _conversationNotifier;
    private readonly ILogger<DeleteConversationHandler> _logger;

    public DeleteConversationHandler(
        IConversationRepository conversationRepository,
        IConversationParticipantRepository participantRepository,
        IRealtimeGroupManager realtimeGroupManager,
        IConversationNotifier conversationNotifier,
        ILogger<DeleteConversationHandler> logger)
    {
        _conversationRepository = conversationRepository;
        _participantRepository = participantRepository;
        _realtimeGroupManager = realtimeGroupManager;
        _conversationNotifier = conversationNotifier;
        _logger = logger;
    }

    public async Task<ApplicationResponse<bool>> HandleAsync(
        DeleteConversationInput request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var access = await _conversationRepository.GetByIdWithParticipantCheckAsync(
            request.ConversationId, currentUserId, cancellationToken);

        if (access is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Conversation.NotFound,
                "Conversation was not found");
        }

        if (access.Participant is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Conversation.AccessDenied,
                "You are not a participant of this conversation");
        }

        if (access.Conversation.Type == ConversationType.Direct)
        {
            access.Participant.Hide();
            await _participantRepository.UpdateAsync(access.Participant, cancellationToken);

            return ApplicationResponse<bool>.Ok(true);
        }

        await BestEffortNotificationHelper.TryNotifyAsync(
            ct => _conversationNotifier.NotifyParticipantLeftAsync(
                new ConversationParticipantLeftNotification(request.ConversationId, currentUserId), ct),
            NotificationTimeout,
            _logger,
            "Failed to notify participants of conversation {ConversationId} that user {UserId} left",
            request.ConversationId,
            currentUserId);

        var remaining = await _participantRepository.RemoveAsync(access.Participant, cancellationToken);

        await BestEffortNotificationHelper.TryNotifyAsync(
            ct => _realtimeGroupManager.RemoveUserFromConversationGroupAsync(currentUserId, request.ConversationId, ct),
            NotificationTimeout,
            _logger,
            "Failed to remove user {UserId} from conversation {ConversationId} SignalR group",
            currentUserId,
            request.ConversationId);

        if (remaining == 0)
        {
            await _conversationRepository.DeleteAsync(request.ConversationId, cancellationToken);
        }

        return ApplicationResponse<bool>.Ok(true);
    }
}
