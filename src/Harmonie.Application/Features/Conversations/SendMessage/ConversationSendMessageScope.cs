using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Services;
using Harmonie.Domain.Common;
using Harmonie.Domain.Entities.Conversations;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Conversations.SendMessage;

/// <summary>
/// Conversation-specific implementation of <see cref="ISendMessageScope{TContext}"/>.
/// </summary>
public sealed class ConversationSendMessageScope : ISendMessageScope<ConversationSendMessageScope.Context>
{
    private static readonly TimeSpan NotificationTimeout = TimeSpan.FromSeconds(5);

    public sealed record Context(
        ConversationId ConversationId,
        string? ConversationName,
        ConversationType ConversationType,
        IReadOnlyList<ConversationParticipant> AllParticipants,
        string CallerUsername,
        string CallerDisplayName) : ScopeContext;

    private readonly ConversationId _conversationId;
    private readonly IConversationRepository _conversationRepository;
    private readonly IConversationParticipantRepository _participantRepository;
    private readonly IConversationMessageNotifier _conversationMessageNotifier;
    private readonly LinkPreviewResolutionService _linkPreviewService;
    private readonly ILogger<ConversationSendMessageScope> _logger;

    public ConversationSendMessageScope(
        ConversationId conversationId,
        IConversationRepository conversationRepository,
        IConversationParticipantRepository participantRepository,
        IConversationMessageNotifier conversationMessageNotifier,
        LinkPreviewResolutionService linkPreviewService,
        ILogger<ConversationSendMessageScope> logger)
    {
        _conversationId = conversationId;
        _conversationRepository = conversationRepository;
        _participantRepository = participantRepository;
        _conversationMessageNotifier = conversationMessageNotifier;
        _linkPreviewService = linkPreviewService;
        _logger = logger;
    }

    public async Task<AuthorizationResult<Context>> AuthorizeAsync(UserId caller, CancellationToken ct)
    {
        var access = await _conversationRepository.GetByIdWithAllParticipantsAsync(_conversationId, caller, ct);
        if (access is null)
        {
            return new AuthorizationResult<Context>.Denied(new ApplicationError(
                ApplicationErrorCodes.Conversation.NotFound,
                "Conversation was not found"));
        }
        if (access.CallerParticipant is null)
        {
            return new AuthorizationResult<Context>.Denied(new ApplicationError(
                ApplicationErrorCodes.Conversation.AccessDenied,
                "You do not have access to this conversation"));
        }

        return new AuthorizationResult<Context>.Authorized(new Context(
            _conversationId,
            access.Conversation.Name,
            access.Conversation.Type,
            access.AllParticipants,
            access.CallerUsername ?? string.Empty,
            access.CallerDisplayName ?? string.Empty));
    }

    public async Task ApplyInTransactionSideEffectsAsync(Context context, CancellationToken ct)
    {
        if (context.ConversationType != ConversationType.Direct)
            return;

        var hidden = context.AllParticipants
            .Where(p => p.HiddenAtUtc is not null)
            .ToArray();

        if (hidden.Length == 0)
            return;

        foreach (var p in hidden)
            p.Unhide();

        await _participantRepository.UpdateRangeAsync(hidden, ct);
    }

    public Task<Result> ValidateMentionedUsersAsync(
        IReadOnlyCollection<UserId> userIds,
        Context context,
        CancellationToken ct)
    {
        var participantIds = context.AllParticipants.Select(p => p.UserId).ToHashSet();
        foreach (var userId in userIds)
        {
            if (!participantIds.Contains(userId))
            {
                return Task.FromResult(Result.Failure($"User {userId.Value} is not a participant of conversation {context.ConversationId.Value}"));
            }
        }

        return Task.FromResult(Result.Success());
    }

    public async Task NotifyMessageCreatedAsync(
        Context context,
        Message message,
        IReadOnlyList<MessageAttachmentDto> attachments,
        ReplyPreviewDto? replyTo,
        CancellationToken ct)
    {
        var notification = new ConversationMessageCreatedNotification(
            message.Id,
            context.ConversationId,
            context.ConversationName,
            context.ConversationType.ToString(),
            message.AuthorUserId,
            context.CallerUsername,
            context.CallerDisplayName,
            message.Content?.Value,
            attachments,
            replyTo,
            message.MentionedUserIds.Select(id => id.Value).ToArray(),
            message.CreatedAtUtc);

        await BestEffortNotificationHelper.TryNotifyAsync(
            token => _conversationMessageNotifier.NotifyMessageCreatedAsync(notification, token),
            NotificationTimeout,
            _logger,
            "SendConversationMessage notification failed (best-effort). MessageId={MessageId}, ConversationId={ConversationId}",
            notification.MessageId,
            notification.ConversationId);
    }

    public void ScheduleLinkPreviewResolution(
        Context context,
        Message message,
        IReadOnlyList<Uri> urls,
        CancellationToken ct)
    {
        // TODO: Replace fire-and-forget with a domain event + dedicated background worker
        _ = _linkPreviewService.ResolveAndNotifyForConversationAsync(
            message.Id,
            context.ConversationId,
            context.ConversationName,
            context.ConversationType.ToString(),
            urls,
            ct);
    }
}
