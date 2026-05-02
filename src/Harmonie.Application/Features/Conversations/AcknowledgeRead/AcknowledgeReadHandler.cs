using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Domain.Entities.Conversations;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Conversations.AcknowledgeRead;

public sealed record AcknowledgeConversationReadInput(ConversationId ConversationId, MessageId? MessageId);

public sealed class AcknowledgeReadHandler : IAuthenticatedHandler<AcknowledgeConversationReadInput, bool>
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IConversationReadStateRepository _conversationReadStateRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AcknowledgeReadHandler(
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        IConversationReadStateRepository conversationReadStateRepository,
        IUnitOfWork unitOfWork)
    {
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _conversationReadStateRepository = conversationReadStateRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResponse<bool>> HandleAsync(
        AcknowledgeConversationReadInput request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var access = await _conversationRepository.GetByIdWithParticipantCheckAsync(request.ConversationId, currentUserId, cancellationToken);
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

        MessageId resolvedMessageId;

        if (request.MessageId is not null)
        {
            var message = await _messageRepository.GetByIdAsync(request.MessageId, cancellationToken);
            if (message is null || message.ConversationId != request.ConversationId)
            {
                return ApplicationResponse<bool>.Fail(
                    ApplicationErrorCodes.Message.NotFound,
                    "Message was not found in this conversation");
            }

            resolvedMessageId = request.MessageId;
        }
        else
        {
            var latestMessageId = await _messageRepository.GetLatestConversationMessageIdAsync(request.ConversationId, cancellationToken);
            if (latestMessageId is null)
            {
                return ApplicationResponse<bool>.Ok(true);
            }

            resolvedMessageId = latestMessageId;
        }

        var state = ConversationReadState.Create(currentUserId, request.ConversationId, resolvedMessageId);
        if (state.IsFailure || state.Value is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                state.Error ?? "Invalid read state");
        }

        await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);
        await _conversationReadStateRepository.UpsertAsync(state.Value, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ApplicationResponse<bool>.Ok(true);
    }
}
