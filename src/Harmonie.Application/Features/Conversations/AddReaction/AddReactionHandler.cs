using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Conversations.AddReaction;

public sealed record ConversationAddReactionInput(ConversationId ConversationId, MessageId MessageId, string Emoji);

public sealed class AddReactionHandler : IAuthenticatedHandler<ConversationAddReactionInput, bool>
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
        ConversationAddReactionInput request,
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

        var isParticipant = await _conversationRepository.IsParticipantAsync(request.ConversationId, currentUserId, cancellationToken);
        if (!isParticipant)
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
                ApplicationErrorCodes.Reaction.MessageNotFound,
                "Message was not found");
        }

        await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);
        await _reactionRepository.AddAsync(request.MessageId, currentUserId, request.Emoji, DateTime.UtcNow, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await NotifyReactionAddedSafelyAsync(
            new ConversationReactionAddedNotification(request.MessageId, request.ConversationId, currentUserId, request.Emoji));

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
