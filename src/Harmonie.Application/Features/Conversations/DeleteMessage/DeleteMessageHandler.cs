using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Features.Conversations.Messages;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Conversations.DeleteMessage;

public sealed record DeleteConversationMessageInput(ConversationId ConversationId, MessageId MessageId);

public sealed class DeleteMessageHandler : IAuthenticatedHandler<DeleteConversationMessageInput, bool>
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IConversationMessageNotifier _conversationMessageNotifier;
    private readonly ILogger<ConversationMessageEditDeleteScope> _scopeLogger;
    private readonly MessageEditDeleteOrchestrator _orchestrator;

    public DeleteMessageHandler(
        IConversationRepository conversationRepository,
        IConversationMessageNotifier conversationMessageNotifier,
        ILogger<ConversationMessageEditDeleteScope> scopeLogger,
        MessageEditDeleteOrchestrator orchestrator)
    {
        _conversationRepository = conversationRepository;
        _conversationMessageNotifier = conversationMessageNotifier;
        _scopeLogger = scopeLogger;
        _orchestrator = orchestrator;
    }

    public async Task<ApplicationResponse<bool>> HandleAsync(
        DeleteConversationMessageInput request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var scope = new ConversationMessageEditDeleteScope(
            request.ConversationId,
            _conversationRepository,
            _conversationMessageNotifier,
            _scopeLogger);

        return await _orchestrator.DeleteAsync(
            scope,
            new MessageScope.Conversation(request.ConversationId),
            request.MessageId,
            currentUserId,
            cancellationToken);
    }
}
