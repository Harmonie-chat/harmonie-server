using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Conversations.EditDirectMessage;

public sealed class EditDirectMessageHandler
{
    private static readonly TimeSpan NotificationTimeout = TimeSpan.FromSeconds(5);

    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _directMessageRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDirectMessageNotifier _directMessageNotifier;
    private readonly ILogger<EditDirectMessageHandler> _logger;

    public EditDirectMessageHandler(
        IConversationRepository conversationRepository,
        IMessageRepository directMessageRepository,
        IUnitOfWork unitOfWork,
        IDirectMessageNotifier directMessageNotifier,
        ILogger<EditDirectMessageHandler> logger)
    {
        _conversationRepository = conversationRepository;
        _directMessageRepository = directMessageRepository;
        _unitOfWork = unitOfWork;
        _directMessageNotifier = directMessageNotifier;
        _logger = logger;
    }

    public async Task<ApplicationResponse<EditDirectMessageResponse>> HandleAsync(
        ConversationId conversationId,
        MessageId messageId,
        EditDirectMessageRequest request,
        UserId callerId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "EditDirectMessage started. ConversationId={ConversationId}, MessageId={MessageId}, CallerId={CallerId}",
            conversationId,
            messageId,
            callerId);

        var contentResult = MessageContent.Create(request.Content);
        if (contentResult.IsFailure || contentResult.Value is null)
        {
            _logger.LogWarning(
                "EditDirectMessage validation failed. ConversationId={ConversationId}, MessageId={MessageId}, CallerId={CallerId}, Error={Error}",
                conversationId,
                messageId,
                callerId,
                contentResult.Error);

            var code = MessageContentErrorCodeResolver.Resolve(request.Content);
            return ApplicationResponse<EditDirectMessageResponse>.Fail(
                code,
                contentResult.Error ?? "Message content is invalid");
        }

        var conversation = await _conversationRepository.GetByIdAsync(conversationId, cancellationToken);
        if (conversation is null)
        {
            _logger.LogWarning(
                "EditDirectMessage failed because conversation was not found. ConversationId={ConversationId}",
                conversationId);

            return ApplicationResponse<EditDirectMessageResponse>.Fail(
                ApplicationErrorCodes.Conversation.NotFound,
                "Conversation was not found");
        }

        if (conversation.User1Id != callerId && conversation.User2Id != callerId)
        {
            _logger.LogWarning(
                "EditDirectMessage access denied because caller is not a participant. ConversationId={ConversationId}, CallerId={CallerId}",
                conversationId,
                callerId);

            return ApplicationResponse<EditDirectMessageResponse>.Fail(
                ApplicationErrorCodes.Conversation.AccessDenied,
                "You do not have access to this conversation");
        }

        var message = await _directMessageRepository.GetByIdAsync(messageId, cancellationToken);
        var messageConversationId = message?.ConversationId;
        if (message is null || messageConversationId is null || messageConversationId != conversationId)
        {
            _logger.LogWarning(
                "EditDirectMessage failed because message was not found. ConversationId={ConversationId}, MessageId={MessageId}",
                conversationId,
                messageId);

            return ApplicationResponse<EditDirectMessageResponse>.Fail(
                ApplicationErrorCodes.Message.NotFound,
                "Message was not found");
        }

        if (message.AuthorUserId != callerId)
        {
            _logger.LogWarning(
                "EditDirectMessage forbidden because caller is not the author. ConversationId={ConversationId}, MessageId={MessageId}, CallerId={CallerId}",
                conversationId,
                messageId,
                callerId);

            return ApplicationResponse<EditDirectMessageResponse>.Fail(
                ApplicationErrorCodes.Message.EditForbidden,
                "You can only edit your own messages");
        }

        var updateResult = message.UpdateContent(contentResult.Value);
        if (updateResult.IsFailure)
        {
            return ApplicationResponse<EditDirectMessageResponse>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                updateResult.Error ?? "Message content update failed");
        }

        await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);
        await _directMessageRepository.UpdateAsync(message, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var updatedAtUtc = message.UpdatedAtUtc;
        if (updatedAtUtc is null)
        {
            return ApplicationResponse<EditDirectMessageResponse>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Message edit succeeded but updated timestamp is missing");
        }

        _logger.LogInformation(
            "EditDirectMessage succeeded. ConversationId={ConversationId}, MessageId={MessageId}, CallerId={CallerId}",
            conversationId,
            messageId,
            callerId);

        await NotifyMessageUpdatedSafelyAsync(
            new DirectMessageUpdatedNotification(
                message.Id,
                messageConversationId,
                message.Content.Value,
                updatedAtUtc.Value));

        return ApplicationResponse<EditDirectMessageResponse>.Ok(new EditDirectMessageResponse(
            MessageId: message.Id.ToString(),
            ConversationId: messageConversationId.ToString(),
            AuthorUserId: message.AuthorUserId.ToString(),
            Content: message.Content.Value,
            CreatedAtUtc: message.CreatedAtUtc,
            UpdatedAtUtc: updatedAtUtc));
    }

    private async Task NotifyMessageUpdatedSafelyAsync(
        DirectMessageUpdatedNotification notification)
    {
        await BestEffortNotificationHelper.TryNotifyAsync(
            token => _directMessageNotifier.NotifyMessageUpdatedAsync(notification, token),
            NotificationTimeout,
            _logger,
            "EditDirectMessage notification failed (best-effort). MessageId={MessageId}, ConversationId={ConversationId}",
            notification.MessageId,
            notification.ConversationId);
    }
}
