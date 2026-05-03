using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Conversations.PinMessage;

public sealed record ConversationPinMessageInput(ConversationId ConversationId, MessageId MessageId);

public sealed class PinMessageHandler : IAuthenticatedHandler<ConversationPinMessageInput, bool>
{
    private static readonly TimeSpan NotificationTimeout = TimeSpan.FromSeconds(5);

    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IPinnedMessageRepository _pinnedMessageRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPinNotifier _pinNotifier;
    private readonly ILogger<PinMessageHandler> _logger;

    public PinMessageHandler(
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        IPinnedMessageRepository pinnedMessageRepository,
        IUnitOfWork unitOfWork,
        IPinNotifier pinNotifier,
        ILogger<PinMessageHandler> logger)
    {
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _pinnedMessageRepository = pinnedMessageRepository;
        _unitOfWork = unitOfWork;
        _pinNotifier = pinNotifier;
        _logger = logger;
    }

    public async Task<ApplicationResponse<bool>> HandleAsync(
        ConversationPinMessageInput request,
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

        var pinnedMessage = PinnedMessage.Create(request.MessageId, currentUserId);
        if (pinnedMessage.IsFailure || pinnedMessage.Value is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                pinnedMessage.Error ?? "Invalid pin");
        }

        await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);
        await _pinnedMessageRepository.AddAsync(pinnedMessage.Value, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await NotifyPinAddedSafelyAsync(
            new ConversationPinAddedNotification(
                request.MessageId,
                request.ConversationId,
                access.Conversation.Name,
                access.Conversation.Type.ToString(),
                currentUserId,
                access.CallerUsername ?? string.Empty,
                access.CallerDisplayName,
                pinnedMessage.Value.PinnedAtUtc));

        return ApplicationResponse<bool>.Ok(true);
    }

    private async Task NotifyPinAddedSafelyAsync(
        ConversationPinAddedNotification notification)
    {
        await BestEffortNotificationHelper.TryNotifyAsync(
            token => _pinNotifier.NotifyMessagePinnedInConversationAsync(notification, token),
            NotificationTimeout,
            _logger,
            "Conversation pin notification failed (best-effort). MessageId={MessageId}, ConversationId={ConversationId}",
            notification.MessageId,
            notification.ConversationId);
    }
}
