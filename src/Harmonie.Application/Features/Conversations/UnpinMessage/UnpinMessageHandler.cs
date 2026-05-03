using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Conversations.UnpinMessage;

public sealed record ConversationUnpinMessageInput(ConversationId ConversationId, MessageId MessageId);

public sealed class UnpinMessageHandler : IAuthenticatedHandler<ConversationUnpinMessageInput, bool>
{
    private static readonly TimeSpan NotificationTimeout = TimeSpan.FromSeconds(5);

    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IPinnedMessageRepository _pinnedMessageRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPinNotifier _pinNotifier;
    private readonly ILogger<UnpinMessageHandler> _logger;

    public UnpinMessageHandler(
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        IPinnedMessageRepository pinnedMessageRepository,
        IUnitOfWork unitOfWork,
        IPinNotifier pinNotifier,
        ILogger<UnpinMessageHandler> logger)
    {
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _pinnedMessageRepository = pinnedMessageRepository;
        _unitOfWork = unitOfWork;
        _pinNotifier = pinNotifier;
        _logger = logger;
    }

    public async Task<ApplicationResponse<bool>> HandleAsync(
        ConversationUnpinMessageInput request,
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
                "You do not have access to this conversation");
        }

        var message = await _messageRepository.GetByIdAsync(request.MessageId, cancellationToken);
        var messageConversationId = message?.ConversationId;
        if (message is null || messageConversationId is null || messageConversationId != request.ConversationId)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Pin.MessageNotFound,
                "Message was not found");
        }

        var isPinned = await _pinnedMessageRepository.IsPinnedAsync(request.MessageId, cancellationToken);
        if (!isPinned)
        {
            return ApplicationResponse<bool>.Ok(true);
        }

        await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);
        await _pinnedMessageRepository.RemoveAsync(request.MessageId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await NotifyPinRemovedSafelyAsync(
            new ConversationPinRemovedNotification(
                request.MessageId,
                request.ConversationId,
                currentUserId,
                access.CallerUsername ?? string.Empty,
                access.CallerDisplayName,
                DateTime.UtcNow));

        return ApplicationResponse<bool>.Ok(true);
    }

    private async Task NotifyPinRemovedSafelyAsync(
        ConversationPinRemovedNotification notification)
    {
        await BestEffortNotificationHelper.TryNotifyAsync(
            token => _pinNotifier.NotifyMessageUnpinnedInConversationAsync(notification, token),
            NotificationTimeout,
            _logger,
            "Conversation unpin notification failed (best-effort). MessageId={MessageId}, ConversationId={ConversationId}",
            notification.MessageId,
            notification.ConversationId);
    }
}
