using Harmonie.Application.Common;
using Harmonie.Application.Features.Users;
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
                    ConversationId: conversation.ConversationId.Value,
                    Type: conversation.Type.ToString().ToLowerInvariant(),
                    Name: conversation.Name,
                    Participants: conversation.Participants
                        .Select(p =>
                        {
                            var avatar = p.AvatarColor is not null || p.AvatarIcon is not null || p.AvatarBg is not null
                                ? new AvatarAppearanceDto(p.AvatarColor, p.AvatarIcon, p.AvatarBg)
                                : null;

                            return new ListConversationsParticipantDto(
                                UserId: p.UserId.Value,
                                Username: p.Username.Value,
                                DisplayName: p.DisplayName,
                                AvatarFileId: p.AvatarFileId,
                                Avatar: avatar);
                        })
                        .ToArray(),
                    CreatedAtUtc: conversation.CreatedAtUtc,
                    HasUnread: conversation.HasUnread))
                .ToArray());

        return ApplicationResponse<ListConversationsResponse>.Ok(payload);
    }
}
