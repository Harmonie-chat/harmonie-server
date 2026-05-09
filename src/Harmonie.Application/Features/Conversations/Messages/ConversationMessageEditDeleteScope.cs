using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Interfaces.Conversations;
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
    private readonly IConversationMessageNotifier _conversationMessageNotifier;
    private readonly ILogger<ConversationMessageEditDeleteScope> _logger;

    public ConversationMessageEditDeleteScope(
        ConversationId conversationId,
        IConversationRepository conversationRepository,
        IConversationMessageNotifier conversationMessageNotifier,
        ILogger<ConversationMessageEditDeleteScope> logger)
    {
        _conversationId = conversationId;
        _conversationRepository = conversationRepository;
        _conversationMessageNotifier = conversationMessageNotifier;
        _logger = logger;
    }

    public async Task<AuthorizationResult<Context>> AuthorizeAsync(UserId caller, CancellationToken ct)
    {
        var access = await _conversationRepository.GetByIdWithParticipantCheckAsync(_conversationId, caller, ct);
        if (access is null)
            return Denied(ApplicationErrorCodes.Conversation.NotFound, "Conversation was not found");

        if (access.Participant is null)
            return Denied(ApplicationErrorCodes.Conversation.AccessDenied, "You do not have access to this conversation");

        return new AuthorizationResult<Context>.Authorized(new Context(
            _conversationId,
            access.Conversation.Name,
            access.Conversation.Type));
    }

    // Conversations have no admin role; only the author can delete their own messages.
    public bool CanDeleteOthersMessages(Context context)
        => false;

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

    private static AuthorizationResult<Context> Denied(string code, string detail)
        => new AuthorizationResult<Context>.Denied(new ApplicationError(code, detail));
}
