using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Features.Conversations.Messages;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Conversations.GetMessages;

public sealed record GetConversationMessagesInput(ConversationId ConversationId, string? Cursor = null, int? Limit = null);

public sealed class GetMessagesHandler : IAuthenticatedHandler<GetConversationMessagesInput, GetMessagesResponse>
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IMessagePaginationRepository _paginationRepository;
    private readonly MessageFetchOrchestrator _orchestrator;

    public GetMessagesHandler(
        IConversationRepository conversationRepository,
        IMessagePaginationRepository paginationRepository,
        MessageFetchOrchestrator orchestrator)
    {
        _conversationRepository = conversationRepository;
        _paginationRepository = paginationRepository;
        _orchestrator = orchestrator;
    }

    public async Task<ApplicationResponse<GetMessagesResponse>> HandleAsync(
        GetConversationMessagesInput request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var scope = new ConversationMessagePageScope(
            request.ConversationId, _conversationRepository, _paginationRepository);

        var result = await _orchestrator.FetchAsync(
            scope, request.Cursor, request.Limit, currentUserId, cancellationToken);

        if (!result.Success)
            return ApplicationResponse<GetMessagesResponse>.Fail(result.Error);

        return ApplicationResponse<GetMessagesResponse>.Ok(new GetMessagesResponse(
            ConversationId: request.ConversationId.Value,
            Items: result.Data.Items,
            NextCursor: result.Data.NextCursor,
            LastReadMessageId: result.Data.LastReadMessageId,
            LastReadAtUtc: result.Data.LastReadAtUtc));
    }
}
