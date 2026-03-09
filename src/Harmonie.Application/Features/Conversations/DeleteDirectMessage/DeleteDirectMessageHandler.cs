using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Conversations.DeleteDirectMessage;

public sealed class DeleteDirectMessageHandler
{
    private static readonly TimeSpan NotificationTimeout = TimeSpan.FromSeconds(5);

    private readonly IConversationRepository _conversationRepository;
    private readonly IDirectMessageRepository _directMessageRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDirectMessageNotifier _directMessageNotifier;
    private readonly ILogger<DeleteDirectMessageHandler> _logger;

    public DeleteDirectMessageHandler(
        IConversationRepository conversationRepository,
        IDirectMessageRepository directMessageRepository,
        IUnitOfWork unitOfWork,
        IDirectMessageNotifier directMessageNotifier,
        ILogger<DeleteDirectMessageHandler> logger)
    {
        _conversationRepository = conversationRepository;
        _directMessageRepository = directMessageRepository;
        _unitOfWork = unitOfWork;
        _directMessageNotifier = directMessageNotifier;
        _logger = logger;
    }

    public async Task<ApplicationResponse<bool>> HandleAsync(
        ConversationId conversationId,
        DirectMessageId messageId,
        UserId callerId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "DeleteDirectMessage started. ConversationId={ConversationId}, MessageId={MessageId}, CallerId={CallerId}",
            conversationId,
            messageId,
            callerId);

        var conversation = await _conversationRepository.GetByIdAsync(conversationId, cancellationToken);
        if (conversation is null)
        {
            _logger.LogWarning(
                "DeleteDirectMessage failed because conversation was not found. ConversationId={ConversationId}",
                conversationId);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Conversation.NotFound,
                "Conversation was not found");
        }

        if (conversation.User1Id != callerId && conversation.User2Id != callerId)
        {
            _logger.LogWarning(
                "DeleteDirectMessage access denied because caller is not a participant. ConversationId={ConversationId}, CallerId={CallerId}",
                conversationId,
                callerId);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Conversation.AccessDenied,
                "You do not have access to this conversation");
        }

        var message = await _directMessageRepository.GetByIdAsync(messageId, cancellationToken);
        if (message is null || message.ConversationId != conversationId)
        {
            _logger.LogWarning(
                "DeleteDirectMessage failed because message was not found. ConversationId={ConversationId}, MessageId={MessageId}",
                conversationId,
                messageId);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Message.NotFound,
                "Message was not found");
        }

        if (message.AuthorUserId != callerId)
        {
            _logger.LogWarning(
                "DeleteDirectMessage forbidden because caller is not the author. ConversationId={ConversationId}, MessageId={MessageId}, CallerId={CallerId}",
                conversationId,
                messageId,
                callerId);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Message.DeleteForbidden,
                "You can only delete your own direct messages");
        }

        var deleteResult = message.Delete();
        if (deleteResult.IsFailure)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                deleteResult.Error ?? "Message deletion failed");
        }

        await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);
        await _directMessageRepository.SoftDeleteAsync(message, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "DeleteDirectMessage succeeded. ConversationId={ConversationId}, MessageId={MessageId}, CallerId={CallerId}",
            conversationId,
            messageId,
            callerId);

        await NotifyMessageDeletedSafelyAsync(
            new DirectMessageDeletedNotification(messageId, conversationId));

        return ApplicationResponse<bool>.Ok(true);
    }

    private async Task NotifyMessageDeletedSafelyAsync(
        DirectMessageDeletedNotification notification)
    {
        await BestEffortNotificationHelper.TryNotifyAsync(
            token => _directMessageNotifier.NotifyMessageDeletedAsync(notification, token),
            NotificationTimeout,
            _logger,
            "DeleteDirectMessage notification failed (best-effort). MessageId={MessageId}, ConversationId={ConversationId}",
            notification.MessageId,
            notification.ConversationId);
    }
}
