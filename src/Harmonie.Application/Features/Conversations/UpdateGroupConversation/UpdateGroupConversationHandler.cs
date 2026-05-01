using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Domain.Entities.Conversations;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Conversations.UpdateGroupConversation;

public sealed record UpdateGroupConversationInput(ConversationId ConversationId, string? Name);

public sealed class UpdateGroupConversationHandler : IAuthenticatedHandler<UpdateGroupConversationInput, UpdateGroupConversationResponse>
{
    private static readonly TimeSpan NotificationTimeout = TimeSpan.FromSeconds(5);

    private readonly IConversationRepository _conversationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConversationNotifier _conversationNotifier;
    private readonly ILogger<UpdateGroupConversationHandler> _logger;

    public UpdateGroupConversationHandler(
        IConversationRepository conversationRepository,
        IUnitOfWork unitOfWork,
        IConversationNotifier conversationNotifier,
        ILogger<UpdateGroupConversationHandler> logger)
    {
        _conversationRepository = conversationRepository;
        _unitOfWork = unitOfWork;
        _conversationNotifier = conversationNotifier;
        _logger = logger;
    }

    public async Task<ApplicationResponse<UpdateGroupConversationResponse>> HandleAsync(
        UpdateGroupConversationInput request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var access = await _conversationRepository.GetByIdWithParticipantCheckAsync(
            request.ConversationId, currentUserId, cancellationToken);

        if (access is null)
        {
            return ApplicationResponse<UpdateGroupConversationResponse>.Fail(
                ApplicationErrorCodes.Conversation.NotFound,
                "Conversation was not found");
        }

        if (access.Participant is null)
        {
            return ApplicationResponse<UpdateGroupConversationResponse>.Fail(
                ApplicationErrorCodes.Conversation.AccessDenied,
                "You are not a participant of this conversation");
        }

        if (access.Conversation.Type != ConversationType.Group)
        {
            return ApplicationResponse<UpdateGroupConversationResponse>.Fail(
                ApplicationErrorCodes.Conversation.InvalidConversationType,
                "Only group conversations can be updated");
        }

        var conversation = access.Conversation;

        if (request.Name is not null)
        {
            var updateResult = conversation.UpdateName(request.Name);
            if (updateResult.IsFailure)
            {
                return ApplicationResponse<UpdateGroupConversationResponse>.Fail(
                    ApplicationErrorCodes.Common.DomainRuleViolation,
                    updateResult.Error ?? "Conversation name update failed");
            }

            await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);
            await _conversationRepository.UpdateAsync(conversation, cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            await BestEffortNotificationHelper.TryNotifyAsync(
                ct => _conversationNotifier.NotifyConversationUpdatedAsync(
                    new ConversationUpdatedNotification(
                        ConversationId: conversation.Id,
                        Name: conversation.Name),
                    ct),
                NotificationTimeout,
                _logger,
                "Failed to notify participants of conversation {ConversationId} update",
                conversation.Id);
        }

        return ApplicationResponse<UpdateGroupConversationResponse>.Ok(
            new UpdateGroupConversationResponse(
                ConversationId: conversation.Id.Value,
                Name: conversation.Name));
    }
}
