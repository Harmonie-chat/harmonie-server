using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Conversations.GetConversationParticipants;

public sealed class GetConversationParticipantsHandler
    : IAuthenticatedHandler<ConversationId, GetConversationParticipantsResponse>
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IUserRepository _userRepository;

    public GetConversationParticipantsHandler(
        IConversationRepository conversationRepository,
        IUserRepository userRepository)
    {
        _conversationRepository = conversationRepository;
        _userRepository = userRepository;
    }

    public async Task<ApplicationResponse<GetConversationParticipantsResponse>> HandleAsync(
        ConversationId conversationId,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var access = await _conversationRepository.GetByIdWithAllParticipantsAsync(
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

        var userIds = access.AllParticipants.Select(p => p.UserId).ToArray();
        var users = await _userRepository.GetManyByIdsAsync(userIds, cancellationToken);
        var userById = users.ToDictionary(u => u.Id);

        var dtos = access.AllParticipants
            .Select(p =>
            {
                userById.TryGetValue(p.UserId, out var user);
                return new ConversationParticipantDto(
                    UserId: p.UserId.Value,
                    Username: user?.Username.Value ?? "Unknown",
                    DisplayName: user?.DisplayName,
                    AvatarFileId: user?.AvatarFileId?.Value,
                    AvatarColor: user?.Avatar?.Color,
                    AvatarIcon: user?.Avatar?.Glyph,
                    AvatarBg: user?.Avatar?.Bg,
                    JoinedAtUtc: p.JoinedAtUtc,
                    IsHidden: p.HiddenAtUtc.HasValue);
            })
            .ToArray();

        return ApplicationResponse<GetConversationParticipantsResponse>.Ok(
            new GetConversationParticipantsResponse(dtos));
    }
}
