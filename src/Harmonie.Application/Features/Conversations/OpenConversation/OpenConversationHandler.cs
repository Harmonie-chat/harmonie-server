using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Conversations.OpenConversation;

public sealed class OpenConversationHandler : IAuthenticatedHandler<OpenConversationRequest, OpenConversationResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IConversationRepository _conversationRepository;
    private readonly IRealtimeGroupManager _realtimeGroupManager;
    private readonly ILogger<OpenConversationHandler> _logger;

    public OpenConversationHandler(
        IUserRepository userRepository,
        IConversationRepository conversationRepository,
        IRealtimeGroupManager realtimeGroupManager,
        ILogger<OpenConversationHandler> logger)
    {
        _userRepository = userRepository;
        _conversationRepository = conversationRepository;
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

        var targetUser = await _userRepository.GetByIdAsync(targetUserId, cancellationToken);
        if (targetUser is null)
        {
            return ApplicationResponse<OpenConversationResponse>.Fail(
                ApplicationErrorCodes.User.NotFound,
                "Target user was not found");
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

        var payload = new OpenConversationResponse(
            ConversationId: result.Conversation.Id.Value,
            Type: "direct",
            ParticipantIds: [currentUserId.Value, targetUserId.Value],
            CreatedAtUtc: result.Conversation.CreatedAtUtc,
            Created: result.WasCreated);

        return ApplicationResponse<OpenConversationResponse>.Ok(payload);
    }
}
