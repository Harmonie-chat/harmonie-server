using Harmonie.Application.Common;
using Harmonie.Application.Features.Users;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Conversations.GetConversationParticipants;

public sealed class GetConversationParticipantsHandler
    : IAuthenticatedHandler<ConversationId, GetConversationParticipantsResponse>
{
    private readonly IConversationRepository _conversationRepository;

    public GetConversationParticipantsHandler(
        IConversationRepository conversationRepository)
    {
        _conversationRepository = conversationRepository;
    }

    public async Task<ApplicationResponse<GetConversationParticipantsResponse>> HandleAsync(
        ConversationId conversationId,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var access = await _conversationRepository.GetParticipantsWithProfilesAsync(
            conversationId, currentUserId, cancellationToken);

        if (access is null)
        {
            return ApplicationResponse<GetConversationParticipantsResponse>.Fail(
                ApplicationErrorCodes.Conversation.NotFound,
                "Conversation was not found");
        }

        if (access.CallerParticipant is null)
        {
            return ApplicationResponse<GetConversationParticipantsResponse>.Fail(
                ApplicationErrorCodes.Conversation.AccessDenied,
                "You do not have access to this conversation");
        }

        var items = access.Participants
            .Select(p =>
            {
                var avatar = p.AvatarColor is not null || p.AvatarIcon is not null || p.AvatarBg is not null
                    ? new AvatarAppearanceDto(p.AvatarColor, p.AvatarIcon, p.AvatarBg)
                    : null;

                return new GetConversationParticipantsItem(
                    UserId: p.UserId,
                    Username: p.Username,
                    DisplayName: p.DisplayName,
                    AvatarFileId: p.AvatarFileId,
                    Avatar: avatar,
                    JoinedAtUtc: p.JoinedAtUtc);
            })
            .ToArray();

        return ApplicationResponse<GetConversationParticipantsResponse>.Ok(
            new GetConversationParticipantsResponse(items));
    }
}
