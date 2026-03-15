using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Conversations.AddReaction;

public sealed class AddReactionHandler
{
    private static readonly TimeSpan NotificationTimeout = TimeSpan.FromSeconds(5);

    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IMessageReactionRepository _reactionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IReactionNotifier _reactionNotifier;
    private readonly ILogger<AddReactionHandler> _logger;

    public AddReactionHandler(
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        IMessageReactionRepository reactionRepository,
        IUnitOfWork unitOfWork,
        IReactionNotifier reactionNotifier,
        ILogger<AddReactionHandler> logger)
    {
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _reactionRepository = reactionRepository;
        _unitOfWork = unitOfWork;
        _reactionNotifier = reactionNotifier;
        _logger = logger;
    }

    public async Task<ApplicationResponse<bool>> HandleAsync(
        ConversationId conversationId,
        MessageId messageId,
        string emoji,
        UserId callerId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "AddConversationReaction started. ConversationId={ConversationId}, MessageId={MessageId}, Emoji={Emoji}, CallerId={CallerId}",
            conversationId,
            messageId,
            emoji,
            callerId);

        var conversation = await _conversationRepository.GetByIdAsync(conversationId, cancellationToken);
        if (conversation is null)
        {
            _logger.LogWarning(
                "AddConversationReaction failed because conversation was not found. ConversationId={ConversationId}",
                conversationId);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Conversation.NotFound,
                "Conversation was not found");
        }

        if (conversation.User1Id != callerId && conversation.User2Id != callerId)
        {
            _logger.LogWarning(
                "AddConversationReaction access denied because caller is not a participant. ConversationId={ConversationId}, CallerId={CallerId}",
                conversationId,
                callerId);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Conversation.AccessDenied,
                "You do not have access to this conversation");
        }

        var message = await _messageRepository.GetByIdAsync(messageId, cancellationToken);
        var messageConversationId = message?.ConversationId;
        if (message is null || messageConversationId is null || messageConversationId != conversationId)
        {
            _logger.LogWarning(
                "AddConversationReaction failed because message was not found. ConversationId={ConversationId}, MessageId={MessageId}",
                conversationId,
                messageId);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Reaction.MessageNotFound,
                "Message was not found");
        }

        await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);
        await _reactionRepository.AddAsync(messageId, callerId, emoji, DateTime.UtcNow, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "AddConversationReaction succeeded. ConversationId={ConversationId}, MessageId={MessageId}, Emoji={Emoji}, CallerId={CallerId}",
            conversationId,
            messageId,
            emoji,
            callerId);

        await NotifyReactionAddedSafelyAsync(
            new ConversationReactionAddedNotification(messageId, conversationId, callerId, emoji));

        return ApplicationResponse<bool>.Ok(true);
    }

    private async Task NotifyReactionAddedSafelyAsync(
        ConversationReactionAddedNotification notification)
    {
        await BestEffortNotificationHelper.TryNotifyAsync(
            token => _reactionNotifier.NotifyReactionAddedToConversationAsync(notification, token),
            NotificationTimeout,
            _logger,
            "AddConversationReaction notification failed (best-effort). MessageId={MessageId}, ConversationId={ConversationId}",
            notification.MessageId,
            notification.ConversationId);
    }
}
