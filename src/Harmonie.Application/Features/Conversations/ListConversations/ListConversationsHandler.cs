using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Conversations.ListConversations;

public sealed class ListConversationsHandler : IAuthenticatedHandler<Unit, ListConversationsResponse>
{
    private readonly IConversationRepository _conversationRepository;

    public ListConversationsHandler(
        IConversationRepository conversationRepository)
    {
        _conversationRepository = conversationRepository;
    }

    public async Task<ApplicationResponse<ListConversationsResponse>> HandleAsync(
        Unit request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var conversations = await _conversationRepository.GetUserConversationsAsync(
            currentUserId,
            cancellationToken);

        var payload = new ListConversationsResponse(
            conversations.Select(conversation => new ListConversationsItemResponse(
                    ConversationId: conversation.ConversationId.ToString(),
                    OtherParticipantUserId: conversation.OtherParticipantUserId.ToString(),
                    OtherParticipantUsername: conversation.OtherParticipantUsername.Value,
                    CreatedAtUtc: conversation.CreatedAtUtc))
                .ToArray());

        return ApplicationResponse<ListConversationsResponse>.Ok(payload);
    }
}
