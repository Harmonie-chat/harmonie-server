using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Conversations.EditMessage;

public sealed record EditConversationMessageInput(ConversationId ConversationId, MessageId MessageId, string Content);

public sealed class EditMessageHandler : IAuthenticatedHandler<EditConversationMessageInput, EditMessageResponse>
{
    private static readonly TimeSpan NotificationTimeout = TimeSpan.FromSeconds(5);

    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _conversationMessageRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConversationMessageNotifier _conversationMessageNotifier;
    private readonly ILogger<EditMessageHandler> _logger;

    public EditMessageHandler(
        IConversationRepository conversationRepository,
        IMessageRepository conversationMessageRepository,
        IUnitOfWork unitOfWork,
        IConversationMessageNotifier conversationMessageNotifier,
        ILogger<EditMessageHandler> logger)
    {
        _conversationRepository = conversationRepository;
        _conversationMessageRepository = conversationMessageRepository;
        _unitOfWork = unitOfWork;
        _conversationMessageNotifier = conversationMessageNotifier;
        _logger = logger;
    }

    public async Task<ApplicationResponse<EditMessageResponse>> HandleAsync(
        EditConversationMessageInput request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var contentResult = MessageContent.Create(request.Content);
        if (contentResult.IsFailure || contentResult.Value is null)
        {
            var code = MessageContentErrorCodeResolver.Resolve(request.Content);
            return ApplicationResponse<EditMessageResponse>.Fail(
                code,
                contentResult.Error ?? "Message content is invalid");
        }

        var access = await _conversationRepository.GetByIdWithParticipantCheckAsync(request.ConversationId, currentUserId, cancellationToken);
        if (access is null)
        {
            return ApplicationResponse<EditMessageResponse>.Fail(
                ApplicationErrorCodes.Conversation.NotFound,
                "Conversation was not found");
        }
        if (access.Participant is null)
        {
            return ApplicationResponse<EditMessageResponse>.Fail(
                ApplicationErrorCodes.Conversation.AccessDenied,
                "You do not have access to this conversation");
        }

        var message = await _conversationMessageRepository.GetByIdAsync(request.MessageId, cancellationToken);
        var messageConversationId = message?.ConversationId;
        if (message is null || messageConversationId is null || messageConversationId != request.ConversationId)
        {
            return ApplicationResponse<EditMessageResponse>.Fail(
                ApplicationErrorCodes.Message.NotFound,
                "Message was not found");
        }

        if (message.AuthorUserId != currentUserId)
        {
            return ApplicationResponse<EditMessageResponse>.Fail(
                ApplicationErrorCodes.Message.EditForbidden,
                "You can only edit your own messages");
        }

        var updateResult = message.UpdateContent(contentResult.Value);
        if (updateResult.IsFailure)
        {
            return ApplicationResponse<EditMessageResponse>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                updateResult.Error ?? "Message content update failed");
        }

        await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);
        await _conversationMessageRepository.UpdateAsync(message, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var updatedAtUtc = message.UpdatedAtUtc;
        if (updatedAtUtc is null)
        {
            return ApplicationResponse<EditMessageResponse>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Message edit succeeded but updated timestamp is missing");
        }

        await NotifyMessageUpdatedSafelyAsync(
            new ConversationMessageUpdatedNotification(
                message.Id,
                messageConversationId,
                message.Content?.Value,
                updatedAtUtc.Value));

        return ApplicationResponse<EditMessageResponse>.Ok(new EditMessageResponse(
            MessageId: message.Id.Value,
            ConversationId: messageConversationId.Value,
            AuthorUserId: message.AuthorUserId.Value,
            Content: message.Content?.Value,
            Attachments: message.Attachments.Select(MessageAttachmentDto.FromDomain).ToArray(),
            CreatedAtUtc: message.CreatedAtUtc,
            UpdatedAtUtc: updatedAtUtc));
    }

    private async Task NotifyMessageUpdatedSafelyAsync(
        ConversationMessageUpdatedNotification notification)
    {
        await BestEffortNotificationHelper.TryNotifyAsync(
            token => _conversationMessageNotifier.NotifyMessageUpdatedAsync(notification, token),
            NotificationTimeout,
            _logger,
            "EditConversationMessage notification failed (best-effort). MessageId={MessageId}, ConversationId={ConversationId}",
            notification.MessageId,
            notification.ConversationId);
    }
}
