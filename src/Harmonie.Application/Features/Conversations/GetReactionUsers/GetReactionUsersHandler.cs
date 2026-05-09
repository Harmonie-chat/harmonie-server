using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Features.Conversations.Reactions;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Conversations.GetReactionUsers;

public sealed record GetConversationReactionUsersInput(
    ConversationId ConversationId,
    MessageId MessageId,
    string Emoji,
    string? Cursor = null,
    int? Limit = null);

public sealed class GetReactionUsersHandler : IAuthenticatedHandler<GetConversationReactionUsersInput, GetReactionUsersResponse>
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IReactionNotifier _reactionNotifier;
    private readonly ILogger<ConversationReactionScope> _scopeLogger;
    private readonly ReactionOrchestrator _orchestrator;

    public GetReactionUsersHandler(
        IConversationRepository conversationRepository,
        IReactionNotifier reactionNotifier,
        ILogger<ConversationReactionScope> scopeLogger,
        ReactionOrchestrator orchestrator)
    {
        _conversationRepository = conversationRepository;
        _reactionNotifier = reactionNotifier;
        _scopeLogger = scopeLogger;
        _orchestrator = orchestrator;
    }

    public async Task<ApplicationResponse<GetReactionUsersResponse>> HandleAsync(
        GetConversationReactionUsersInput request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var scope = new ConversationReactionScope(
            request.ConversationId, _conversationRepository, _reactionNotifier, _scopeLogger);

        var result = await _orchestrator.GetUsersAsync(
            scope,
            new MessageScope.Conversation(request.ConversationId),
            request.MessageId,
            request.Emoji,
            request.Cursor,
            request.Limit,
            currentUserId,
            cancellationToken);

        if (!result.Success)
            return ApplicationResponse<GetReactionUsersResponse>.Fail(result.Error);

        return ApplicationResponse<GetReactionUsersResponse>.Ok(new GetReactionUsersResponse(
            result.Data.MessageId,
            result.Data.Emoji,
            result.Data.TotalCount,
            result.Data.Users,
            result.Data.NextCursor));
    }
}
