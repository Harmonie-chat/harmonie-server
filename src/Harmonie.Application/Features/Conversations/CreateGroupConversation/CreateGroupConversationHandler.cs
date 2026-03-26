using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Conversations.CreateGroupConversation;

public sealed class CreateGroupConversationHandler : IAuthenticatedHandler<CreateGroupConversationRequest, CreateGroupConversationResponse>
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IRealtimeGroupManager _realtimeGroupManager;
    private readonly ILogger<CreateGroupConversationHandler> _logger;

    public CreateGroupConversationHandler(
        IConversationRepository conversationRepository,
        IUserRepository userRepository,
        IRealtimeGroupManager realtimeGroupManager,
        ILogger<CreateGroupConversationHandler> logger)
    {
        _conversationRepository = conversationRepository;
        _userRepository = userRepository;
        _realtimeGroupManager = realtimeGroupManager;
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

        foreach (var participantId in participantUserIds)
        {
            var user = await _userRepository.GetByIdAsync(participantId, cancellationToken);
            if (user is null)
            {
                return ApplicationResponse<CreateGroupConversationResponse>.Fail(
                    ApplicationErrorCodes.User.NotFound,
                    $"User {participantId} was not found");
            }
        }

        var conversation = await _conversationRepository.CreateGroupAsync(
            request.Name,
            participantUserIds,
            cancellationToken);

        await BestEffortNotificationHelper.TryNotifyAsync(
            async ct =>
            {
                await Task.WhenAll(
                    participantUserIds.Select(uid =>
                        _realtimeGroupManager.AddUserToConversationGroupAsync(uid, conversation.Id, ct)));
            },
            TimeSpan.FromSeconds(5),
            _logger,
            "Failed to subscribe users to group conversation {ConversationId} SignalR group",
            conversation.Id);

        var payload = new CreateGroupConversationResponse(
            ConversationId: conversation.Id.ToString(),
            Type: "group",
            Name: conversation.Name,
            ParticipantIds: participantUserIds.Select(id => id.ToString()).ToArray(),
            CreatedAtUtc: conversation.CreatedAtUtc);

        return ApplicationResponse<CreateGroupConversationResponse>.Ok(payload);
    }
}
