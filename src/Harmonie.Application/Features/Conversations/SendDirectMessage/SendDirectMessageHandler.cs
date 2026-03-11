using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Conversations.SendDirectMessage;

public sealed class SendDirectMessageHandler
{
    private static readonly TimeSpan NotificationTimeout = TimeSpan.FromSeconds(5);

    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _directMessageRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDirectMessageNotifier _directMessageNotifier;
    private readonly ILogger<SendDirectMessageHandler> _logger;

    public SendDirectMessageHandler(
        IConversationRepository conversationRepository,
        IMessageRepository directMessageRepository,
        IUnitOfWork unitOfWork,
        IDirectMessageNotifier directMessageNotifier,
        ILogger<SendDirectMessageHandler> logger)
    {
        _conversationRepository = conversationRepository;
        _directMessageRepository = directMessageRepository;
        _unitOfWork = unitOfWork;
        _directMessageNotifier = directMessageNotifier;
        _logger = logger;
    }

    public async Task<ApplicationResponse<SendDirectMessageResponse>> HandleAsync(
        ConversationId conversationId,
        SendDirectMessageRequest request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "SendDirectMessage started. ConversationId={ConversationId}, UserId={UserId}",
            conversationId,
            currentUserId);

        var contentResult = MessageContent.Create(request.Content);
        if (contentResult.IsFailure || contentResult.Value is null)
        {
            _logger.LogWarning(
                "SendDirectMessage validation failed. ConversationId={ConversationId}, UserId={UserId}, Error={Error}",
                conversationId,
                currentUserId,
                contentResult.Error);

            var code = MessageContentErrorCodeResolver.Resolve(request.Content);
            return ApplicationResponse<SendDirectMessageResponse>.Fail(
                code,
                contentResult.Error ?? "Message content is invalid");
        }

        var conversation = await _conversationRepository.GetByIdAsync(conversationId, cancellationToken);
        if (conversation is null)
        {
            _logger.LogWarning(
                "SendDirectMessage failed because conversation was not found. ConversationId={ConversationId}, UserId={UserId}",
                conversationId,
                currentUserId);

            return ApplicationResponse<SendDirectMessageResponse>.Fail(
                ApplicationErrorCodes.Conversation.NotFound,
                "Conversation was not found");
        }

        if (conversation.User1Id != currentUserId && conversation.User2Id != currentUserId)
        {
            _logger.LogWarning(
                "SendDirectMessage access denied. ConversationId={ConversationId}, UserId={UserId}",
                conversationId,
                currentUserId);

            return ApplicationResponse<SendDirectMessageResponse>.Fail(
                ApplicationErrorCodes.Conversation.AccessDenied,
                "You do not have access to this conversation");
        }

        var messageResult = Message.CreateForConversation(
            conversationId,
            currentUserId,
            contentResult.Value);
        if (messageResult.IsFailure || messageResult.Value is null)
        {
            _logger.LogWarning(
                "SendDirectMessage domain creation failed. ConversationId={ConversationId}, UserId={UserId}, Error={Error}",
                conversationId,
                currentUserId,
                messageResult.Error);

            return ApplicationResponse<SendDirectMessageResponse>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                messageResult.Error ?? "Unable to create direct message");
        }

        await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);
        await _directMessageRepository.AddAsync(messageResult.Value, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var messageConversationId = messageResult.Value.ConversationId;
        if (messageConversationId is null)
        {
            return ApplicationResponse<SendDirectMessageResponse>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Direct message creation succeeded but conversation ID is missing");
        }

        await NotifyMessageCreatedSafelyAsync(
            new DirectMessageCreatedNotification(
                messageResult.Value.Id,
                messageConversationId,
                messageResult.Value.AuthorUserId,
                messageResult.Value.Content.Value,
                messageResult.Value.CreatedAtUtc));

        _logger.LogInformation(
            "SendDirectMessage succeeded. MessageId={MessageId}, ConversationId={ConversationId}, UserId={UserId}",
            messageResult.Value.Id,
            messageConversationId,
            messageResult.Value.AuthorUserId);

        return ApplicationResponse<SendDirectMessageResponse>.Ok(new SendDirectMessageResponse(
            MessageId: messageResult.Value.Id.ToString(),
            ConversationId: messageConversationId.ToString(),
            AuthorUserId: messageResult.Value.AuthorUserId.ToString(),
            Content: messageResult.Value.Content.Value,
            CreatedAtUtc: messageResult.Value.CreatedAtUtc));
    }

    private async Task NotifyMessageCreatedSafelyAsync(
        DirectMessageCreatedNotification notification)
    {
        await BestEffortNotificationHelper.TryNotifyAsync(
            token => _directMessageNotifier.NotifyMessageCreatedAsync(notification, token),
            NotificationTimeout,
            _logger,
            "SendDirectMessage notification failed (best-effort). MessageId={MessageId}, ConversationId={ConversationId}",
            notification.MessageId,
            notification.ConversationId);
    }
}
