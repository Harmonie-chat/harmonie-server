using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Conversations.DeleteMessage;

public sealed record DeleteConversationMessageInput(ConversationId ConversationId, MessageId MessageId);

public sealed class DeleteMessageHandler : IAuthenticatedHandler<DeleteConversationMessageInput, bool>
{
    private static readonly TimeSpan NotificationTimeout = TimeSpan.FromSeconds(5);

    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _conversationMessageRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConversationMessageNotifier _conversationMessageNotifier;
    private readonly ILogger<DeleteMessageHandler> _logger;

    public DeleteMessageHandler(
        IConversationRepository conversationRepository,
        IMessageRepository conversationMessageRepository,
        IUnitOfWork unitOfWork,
        IConversationMessageNotifier conversationMessageNotifier,
        ILogger<DeleteMessageHandler> logger)
    {
        _conversationRepository = conversationRepository;
        _conversationMessageRepository = conversationMessageRepository;
        _unitOfWork = unitOfWork;
        _conversationMessageNotifier = conversationMessageNotifier;
        _logger = logger;
    }

    public async Task<ApplicationResponse<bool>> HandleAsync(
        DeleteConversationMessageInput request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var conversation = await _conversationRepository.GetByIdAsync(request.ConversationId, cancellationToken);
        if (conversation is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Conversation.NotFound,
                "Conversation was not found");
        }

        if (conversation.User1Id != currentUserId && conversation.User2Id != currentUserId)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Conversation.AccessDenied,
                "You do not have access to this conversation");
        }

        var message = await _conversationMessageRepository.GetByIdAsync(request.MessageId, cancellationToken);
        var messageConversationId = message?.ConversationId;
        if (message is null || messageConversationId is null || messageConversationId != request.ConversationId)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Message.NotFound,
                "Message was not found");
        }

        if (message.AuthorUserId != currentUserId)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Message.DeleteForbidden,
                "You can only delete your own messages");
        }

        var deleteResult = message.Delete();
        if (deleteResult.IsFailure)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                deleteResult.Error ?? "Message deletion failed");
        }

        await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);
        await _conversationMessageRepository.SoftDeleteAsync(message, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await NotifyMessageDeletedSafelyAsync(
            new ConversationMessageDeletedNotification(request.MessageId, request.ConversationId));

        return ApplicationResponse<bool>.Ok(true);
    }

    private async Task NotifyMessageDeletedSafelyAsync(
        ConversationMessageDeletedNotification notification)
    {
        await BestEffortNotificationHelper.TryNotifyAsync(
            token => _conversationMessageNotifier.NotifyMessageDeletedAsync(notification, token),
            NotificationTimeout,
            _logger,
            "DeleteConversationMessage notification failed (best-effort). MessageId={MessageId}, ConversationId={ConversationId}",
            notification.MessageId,
            notification.ConversationId);
    }
}
