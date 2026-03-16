using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Conversations.OpenConversation;

public sealed class OpenConversationHandler
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
        UserId callerUserId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "OpenConversation started. CallerUserId={CallerUserId}, TargetUserIdRaw={TargetUserIdRaw}",
            callerUserId,
            request.TargetUserId);

        if (!UserId.TryParse(request.TargetUserId, out var targetUserId) || targetUserId is null)
        {
            _logger.LogWarning(
                "OpenConversation validation failed. CallerUserId={CallerUserId}, TargetUserIdRaw={TargetUserIdRaw}",
                callerUserId,
                request.TargetUserId);

            return ApplicationResponse<OpenConversationResponse>.Fail(
                ApplicationErrorCodes.Common.ValidationFailed,
                "Request validation failed",
                EndpointExtensions.SingleValidationError(
                    nameof(request.TargetUserId),
                    ApplicationErrorCodes.Validation.InvalidFormat,
                    "Target user ID must be a valid non-empty GUID"));
        }

        if (targetUserId == callerUserId)
        {
            _logger.LogWarning(
                "OpenConversation rejected because caller targeted self. CallerUserId={CallerUserId}",
                callerUserId);

            return ApplicationResponse<OpenConversationResponse>.Fail(
                ApplicationErrorCodes.Conversation.CannotOpenSelf,
                "You cannot open a conversation with yourself");
        }

        var targetUser = await _userRepository.GetByIdAsync(targetUserId, cancellationToken);
        if (targetUser is null)
        {
            _logger.LogWarning(
                "OpenConversation target user not found. CallerUserId={CallerUserId}, TargetUserId={TargetUserId}",
                callerUserId,
                targetUserId);

            return ApplicationResponse<OpenConversationResponse>.Fail(
                ApplicationErrorCodes.User.NotFound,
                "Target user was not found");
        }

        var result = await _conversationRepository.GetOrCreateAsync(
            callerUserId,
            targetUserId,
            cancellationToken);

        _logger.LogInformation(
            "OpenConversation succeeded. ConversationId={ConversationId}, CallerUserId={CallerUserId}, TargetUserId={TargetUserId}, Created={Created}",
            result.Conversation.Id,
            callerUserId,
            targetUserId,
            result.WasCreated);

        if (result.WasCreated)
        {
            var conversationId = result.Conversation.Id;
            await BestEffortNotificationHelper.TryNotifyAsync(
                async ct =>
                {
                    await Task.WhenAll(
                        _realtimeGroupManager.AddUserToConversationGroupAsync(callerUserId, conversationId, ct),
                        _realtimeGroupManager.AddUserToConversationGroupAsync(targetUserId, conversationId, ct));
                },
                TimeSpan.FromSeconds(5),
                _logger,
                "Failed to subscribe users to conversation {ConversationId} SignalR group",
                conversationId);
        }

        var payload = new OpenConversationResponse(
            ConversationId: result.Conversation.Id.ToString(),
            User1Id: result.Conversation.User1Id.ToString(),
            User2Id: result.Conversation.User2Id.ToString(),
            CreatedAtUtc: result.Conversation.CreatedAtUtc,
            Created: result.WasCreated);

        return ApplicationResponse<OpenConversationResponse>.Ok(payload);
    }
}
