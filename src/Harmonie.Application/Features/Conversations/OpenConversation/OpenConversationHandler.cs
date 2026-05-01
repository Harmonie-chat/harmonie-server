using Harmonie.Application.Common;
using Harmonie.Application.Features.Users;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Domain.Entities.Users;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Conversations.OpenConversation;

public sealed class OpenConversationHandler : IAuthenticatedHandler<OpenConversationRequest, OpenConversationResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IConversationRepository _conversationRepository;
    private readonly IConversationParticipantRepository _participantRepository;
    private readonly IRealtimeGroupManager _realtimeGroupManager;
    private readonly ILogger<OpenConversationHandler> _logger;

    public OpenConversationHandler(
        IUserRepository userRepository,
        IConversationRepository conversationRepository,
        IConversationParticipantRepository participantRepository,
        IRealtimeGroupManager realtimeGroupManager,
        ILogger<OpenConversationHandler> logger)
    {
        _userRepository = userRepository;
        _conversationRepository = conversationRepository;
        _participantRepository = participantRepository;
        _realtimeGroupManager = realtimeGroupManager;
        _logger = logger;
    }

    public async Task<ApplicationResponse<OpenConversationResponse>> HandleAsync(
        OpenConversationRequest request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var targetUserId = UserId.From(request.TargetUserId);

        if (targetUserId == currentUserId)
        {
            return ApplicationResponse<OpenConversationResponse>.Fail(
                ApplicationErrorCodes.Conversation.CannotOpenSelf,
                "You cannot open a conversation with yourself");
        }

        var users = await _userRepository.GetManyByIdsAsync([currentUserId, targetUserId], cancellationToken);
        var currentUser = users.FirstOrDefault(u => u.Id == currentUserId);
        var targetUser = users.FirstOrDefault(u => u.Id == targetUserId);

        if (targetUser is null)
        {
            return ApplicationResponse<OpenConversationResponse>.Fail(
                ApplicationErrorCodes.User.NotFound,
                "Target user was not found");
        }

        if (currentUser is null)
        {
            return ApplicationResponse<OpenConversationResponse>.Fail(
                ApplicationErrorCodes.User.NotFound,
                "Current user was not found");
        }

        var result = await _conversationRepository.GetOrCreateDirectAsync(
            currentUserId,
            targetUserId,
            cancellationToken);

        if (result.WasCreated)
        {
            var conversationId = result.Conversation.Id;
            await BestEffortNotificationHelper.TryNotifyAsync(
                async ct =>
                {
                    await Task.WhenAll(
                        _realtimeGroupManager.AddUserToConversationGroupAsync(currentUserId, conversationId, ct),
                        _realtimeGroupManager.AddUserToConversationGroupAsync(targetUserId, conversationId, ct));
                },
                TimeSpan.FromSeconds(5),
                _logger,
                "Failed to subscribe users to conversation {ConversationId} SignalR group",
                conversationId);
        }
        else
        {
            // Reopen: clear hidden_at_utc for both participants so the conversation reappears
            var conversationId = result.Conversation.Id;

            var currentParticipant = await _participantRepository.GetAsync(conversationId, currentUserId, cancellationToken);
            if (currentParticipant is { HiddenAtUtc: not null })
            {
                currentParticipant.Unhide();
                await _participantRepository.UpdateAsync(currentParticipant, cancellationToken);
            }

            var targetParticipant = await _participantRepository.GetAsync(conversationId, targetUserId, cancellationToken);
            if (targetParticipant is { HiddenAtUtc: not null })
            {
                targetParticipant.Unhide();
                await _participantRepository.UpdateAsync(targetParticipant, cancellationToken);
            }
        }

        var payload = new OpenConversationResponse(
            ConversationId: result.Conversation.Id.Value,
            Type: "direct",
            Participants: [ToParticipantDto(currentUser), ToParticipantDto(targetUser)],
            CreatedAtUtc: result.Conversation.CreatedAtUtc,
            Created: result.WasCreated);

        return ApplicationResponse<OpenConversationResponse>.Ok(payload);
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
