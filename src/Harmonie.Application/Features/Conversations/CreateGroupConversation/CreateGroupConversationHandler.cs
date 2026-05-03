using Harmonie.Application.Common;
using Harmonie.Application.Features.Users;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Domain.Entities.Users;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Conversations.CreateGroupConversation;

public sealed class CreateGroupConversationHandler : IAuthenticatedHandler<CreateGroupConversationRequest, CreateGroupConversationResponse>
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IRealtimeGroupManager _realtimeGroupManager;
    private readonly IConversationNotifier _conversationNotifier;
    private readonly ILogger<CreateGroupConversationHandler> _logger;

    public CreateGroupConversationHandler(
        IConversationRepository conversationRepository,
        IUserRepository userRepository,
        IRealtimeGroupManager realtimeGroupManager,
        IConversationNotifier conversationNotifier,
        ILogger<CreateGroupConversationHandler> logger)
    {
        _conversationRepository = conversationRepository;
        _userRepository = userRepository;
        _realtimeGroupManager = realtimeGroupManager;
        _conversationNotifier = conversationNotifier;
        _logger = logger;
    }

    public async Task<ApplicationResponse<CreateGroupConversationResponse>> HandleAsync(
        CreateGroupConversationRequest request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var participantUserIds = request.ParticipantUserIds.Select(UserId.From).ToArray();

        if (!participantUserIds.Any(id => id == currentUserId))
        {
            return ApplicationResponse<CreateGroupConversationResponse>.Fail(
                ApplicationErrorCodes.Conversation.AccessDenied,
                "You must be included in the participant list");
        }

        var participantUsers = await _userRepository.GetManyByIdsAsync(participantUserIds, cancellationToken);
        var missingId = participantUserIds.FirstOrDefault(id => participantUsers.All(u => u.Id != id));
        if (missingId != default)
        {
            return ApplicationResponse<CreateGroupConversationResponse>.Fail(
                ApplicationErrorCodes.User.NotFound,
                $"User {missingId} was not found");
        }

        var conversation = await _conversationRepository.CreateGroupAsync(
            request.Name,
            participantUserIds,
            cancellationToken);

        var participantDtos = participantUsers.Select(ToParticipantDto).ToArray();

        await BestEffortNotificationHelper.TryNotifyAsync(
            async ct =>
            {
                await Task.WhenAll(
                    participantUserIds.Select(uid =>
                        _realtimeGroupManager.AddUserToConversationGroupAsync(uid, conversation.Id, ct)));

                await _conversationNotifier.NotifyConversationCreatedAsync(
                    new ConversationCreatedNotification(
                        ConversationId: conversation.Id,
                        Name: conversation.Name,
                        ConversationType: conversation.Type.ToString(),
                        Participants: participantDtos),
                    ct);
            },
            TimeSpan.FromSeconds(5),
            _logger,
            "Failed to notify participants of group conversation {ConversationId} creation",
            conversation.Id);

        var payload = new CreateGroupConversationResponse(
            ConversationId: conversation.Id.Value,
            Type: "group",
            Name: conversation.Name,
            Participants: participantDtos,
            CreatedAtUtc: conversation.CreatedAtUtc);

        return ApplicationResponse<CreateGroupConversationResponse>.Ok(payload);
    }

    private static ConversationParticipantDto ToParticipantDto(User user)
    {
        var avatar = user.AvatarColor is not null || user.AvatarIcon is not null || user.AvatarBg is not null
            ? new AvatarAppearanceDto(user.AvatarColor, user.AvatarIcon, user.AvatarBg)
            : null;

        return new ConversationParticipantDto(
            UserId: user.Id.Value,
            Username: user.Username.Value,
            DisplayName: user.DisplayName,
            AvatarFileId: user.AvatarFileId?.Value,
            Avatar: avatar);
    }
}
