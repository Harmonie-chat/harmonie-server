using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Domain.Common;
using Harmonie.Domain.Entities.Conversations;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Conversations.Messages;

/// <summary>
/// Conversation-specific implementation of <see cref="IMessageEditDeleteScope{TContext}"/>.
/// </summary>
public sealed class ConversationMessageEditDeleteScope : IMessageEditDeleteScope<ConversationMessageEditDeleteScope.Context>
{
    private static readonly TimeSpan NotificationTimeout = TimeSpan.FromSeconds(5);

    public sealed record Context(
        ConversationId ConversationId,
        string? ConversationName,
        ConversationType ConversationType) : ScopeContext;

    private readonly ConversationId _conversationId;
    private readonly IConversationRepository _conversationRepository;
    private readonly IConversationParticipantRepository _participantRepository;
    private readonly IConversationMessageNotifier _conversationMessageNotifier;
    private readonly ILogger<ConversationMessageEditDeleteScope> _logger;

    public ConversationMessageEditDeleteScope(
        ConversationId conversationId,
        IConversationRepository conversationRepository,
        IConversationParticipantRepository participantRepository,
        IConversationMessageNotifier conversationMessageNotifier,
        ILogger<ConversationMessageEditDeleteScope> logger)
    {
        _conversationId = conversationId;
        _conversationRepository = conversationRepository;
        _participantRepository = participantRepository;
        _conversationMessageNotifier = conversationMessageNotifier;
        _logger = logger;
    }

    public async Task<AuthorizationResult<Context>> AuthorizeAsync(UserId caller, CancellationToken ct)
    {
        var result = await ConversationScopeAuthorizer.AuthorizeAsync(_conversationRepository, _conversationId, caller, ct);
        if (result is ConversationAuthResult.Denied denied)
            return new AuthorizationResult<Context>.Denied(denied.Error);

        var access = ((ConversationAuthResult.Authorized)result).Context;
        return new AuthorizationResult<Context>.Authorized(new Context(
            _conversationId,
            access.Conversation.Name,
            access.Conversation.Type));
    }

    // Conversations have no admin role; only the author can delete their own messages.
    public bool CanDeleteOthersMessages(Context context)
        => false;

    public async Task<Result> ValidateMentionedUsersAsync(
        IReadOnlyCollection<UserId> userIds,
        Context context,
        CancellationToken ct)
    {
        var participants = await _participantRepository.GetByConversationIdAsync(context.ConversationId, ct);
        var participantIds = participants.Select(p => p.UserId).ToHashSet();
        foreach (var userId in userIds)
        {
            if (!participantIds.Contains(userId))
            {
                return Result.Failure($"User {userId.Value} is not a participant of conversation {context.ConversationId.Value}");
            }
        }

        return Result.Success();
    }

    public async Task NotifyMessageUpdatedAsync(
        Context context,
        MessageId messageId,
        string? content,
        DateTime updatedAtUtc,
        CancellationToken ct)
    {
        var notification = new ConversationMessageUpdatedNotification(
            messageId,
            context.ConversationId,
            context.ConversationName,
            context.ConversationType.ToString(),
            content,
            updatedAtUtc);

        await BestEffortNotificationHelper.TryNotifyAsync(
            token => _conversationMessageNotifier.NotifyMessageUpdatedAsync(notification, token),
            NotificationTimeout,
            _logger,
            "EditConversationMessage notification failed (best-effort). MessageId={MessageId}, ConversationId={ConversationId}",
            notification.MessageId,
            notification.ConversationId);
    }

    public async Task NotifyMessageDeletedAsync(
        Context context,
        MessageId messageId,
        CancellationToken ct)
    {
        var notification = new ConversationMessageDeletedNotification(
            messageId,
            context.ConversationId,
            context.ConversationName,
            context.ConversationType.ToString());

        await BestEffortNotificationHelper.TryNotifyAsync(
            token => _conversationMessageNotifier.NotifyMessageDeletedAsync(notification, token),
            NotificationTimeout,
            _logger,
            "DeleteConversationMessage notification failed (best-effort). MessageId={MessageId}, ConversationId={ConversationId}",
            notification.MessageId,
            notification.ConversationId);
    }
}
