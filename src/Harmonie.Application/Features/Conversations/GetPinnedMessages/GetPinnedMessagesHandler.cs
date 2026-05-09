using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Features.Conversations.Pins;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Conversations.GetPinnedMessages;

public sealed record GetConversationPinnedMessagesInput(ConversationId ConversationId, string? Before = null, int? Limit = null);

public sealed class GetPinnedMessagesHandler : IAuthenticatedHandler<GetConversationPinnedMessagesInput, GetConversationPinnedMessagesResponse>
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IPinnedMessageRepository _pinnedMessageRepository;
    private readonly PinnedMessageFetchOrchestrator _orchestrator;

    public GetPinnedMessagesHandler(
        IConversationRepository conversationRepository,
        IPinnedMessageRepository pinnedMessageRepository,
        PinnedMessageFetchOrchestrator orchestrator)
    {
        _conversationRepository = conversationRepository;
        _pinnedMessageRepository = pinnedMessageRepository;
        _orchestrator = orchestrator;
    }

    public async Task<ApplicationResponse<GetConversationPinnedMessagesResponse>> HandleAsync(
        GetConversationPinnedMessagesInput request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var scope = new ConversationPinnedMessageFetchScope(
            request.ConversationId, _conversationRepository, _pinnedMessageRepository);

        var result = await _orchestrator.FetchAsync(
            scope, request.Before, request.Limit, currentUserId, cancellationToken);

        if (!result.Success)
            return ApplicationResponse<GetConversationPinnedMessagesResponse>.Fail(result.Error);

        return ApplicationResponse<GetConversationPinnedMessagesResponse>.Ok(new GetConversationPinnedMessagesResponse(
            ConversationId: request.ConversationId.Value,
            Items: result.Data.Items,
            NextCursor: result.Data.NextCursor));
    }
}
