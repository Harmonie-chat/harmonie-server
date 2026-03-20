using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Conversations.AcknowledgeRead;

public sealed class AcknowledgeReadHandler
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IConversationReadStateRepository _conversationReadStateRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AcknowledgeReadHandler> _logger;

    public AcknowledgeReadHandler(
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        IConversationReadStateRepository conversationReadStateRepository,
        IUnitOfWork unitOfWork,
        ILogger<AcknowledgeReadHandler> logger)
    {
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _conversationReadStateRepository = conversationReadStateRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApplicationResponse<bool>> HandleAsync(
        ConversationId conversationId,
        MessageId? messageId,
        UserId callerId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "AcknowledgeConversationRead started. ConversationId={ConversationId}, MessageId={MessageId}, CallerId={CallerId}",
            conversationId,
            messageId,
            callerId);

        var conversation = await _conversationRepository.GetByIdAsync(conversationId, cancellationToken);
        if (conversation is null)
        {
            _logger.LogWarning(
                "AcknowledgeConversationRead failed because conversation was not found. ConversationId={ConversationId}",
                conversationId);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Conversation.NotFound,
                "Conversation was not found");
        }

        if (conversation.User1Id != callerId && conversation.User2Id != callerId)
        {
            _logger.LogWarning(
                "AcknowledgeConversationRead access denied because caller is not a participant. ConversationId={ConversationId}, CallerId={CallerId}",
                conversationId,
                callerId);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Conversation.AccessDenied,
                "You do not have access to this conversation");
        }

        MessageId resolvedMessageId;

        if (messageId is not null)
        {
            var message = await _messageRepository.GetByIdAsync(messageId, cancellationToken);
            if (message is null || message.ConversationId != conversationId)
            {
                _logger.LogWarning(
                    "AcknowledgeConversationRead failed because message was not found. ConversationId={ConversationId}, MessageId={MessageId}",
                    conversationId,
                    messageId);

                return ApplicationResponse<bool>.Fail(
                    ApplicationErrorCodes.Message.NotFound,
                    "Message was not found in this conversation");
            }

            resolvedMessageId = messageId;
        }
        else
        {
            var latestMessageId = await _messageRepository.GetLatestConversationMessageIdAsync(conversationId, cancellationToken);
            if (latestMessageId is null)
            {
                _logger.LogInformation(
                    "AcknowledgeConversationRead no-op because conversation has no messages. ConversationId={ConversationId}",
                    conversationId);

                return ApplicationResponse<bool>.Ok(true);
            }

            resolvedMessageId = latestMessageId;
        }

        await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);
        await _conversationReadStateRepository.UpsertAsync(callerId, conversationId, resolvedMessageId, DateTime.UtcNow, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "AcknowledgeConversationRead succeeded. ConversationId={ConversationId}, MessageId={MessageId}, CallerId={CallerId}",
            conversationId,
            resolvedMessageId,
            callerId);

        return ApplicationResponse<bool>.Ok(true);
    }
}
