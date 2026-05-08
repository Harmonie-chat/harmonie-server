using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Services;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Domain.Entities.Conversations;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Conversations.SendMessage;

public sealed record SendConversationMessageInput(ConversationId ConversationId, string? Content, IReadOnlyList<Guid>? AttachmentFileIds = null, Guid? ReplyToMessageId = null);

public sealed class SendMessageHandler : IAuthenticatedHandler<SendConversationMessageInput, SendMessageResponse>
{
    private static readonly TimeSpan NotificationTimeout = TimeSpan.FromSeconds(5);

    private readonly IConversationRepository _conversationRepository;
    private readonly IConversationParticipantRepository _participantRepository;
    private readonly IMessageRepository _conversationMessageRepository;
    private readonly IMessageAttachmentRepository _messageAttachmentRepository;
    private readonly MessageAttachmentResolver _messageAttachmentResolver;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConversationMessageNotifier _conversationMessageNotifier;
    private readonly LinkPreviewResolutionService _linkPreviewService;
    private readonly IMessageRepository _messageRepository;
    private readonly ILogger<SendMessageHandler> _logger;

    public SendMessageHandler(
        IConversationRepository conversationRepository,
        IConversationParticipantRepository participantRepository,
        IMessageRepository conversationMessageRepository,
        IMessageAttachmentRepository messageAttachmentRepository,
        MessageAttachmentResolver messageAttachmentResolver,
        IUnitOfWork unitOfWork,
        IConversationMessageNotifier conversationMessageNotifier,
        LinkPreviewResolutionService linkPreviewService,
        IMessageRepository messageRepository,
        ILogger<SendMessageHandler> logger)
    {
        _conversationRepository = conversationRepository;
        _participantRepository = participantRepository;
        _conversationMessageRepository = conversationMessageRepository;
        _messageAttachmentRepository = messageAttachmentRepository;
        _messageAttachmentResolver = messageAttachmentResolver;
        _unitOfWork = unitOfWork;
        _conversationMessageNotifier = conversationMessageNotifier;
        _linkPreviewService = linkPreviewService;
        _messageRepository = messageRepository;
        _logger = logger;
    }

    public async Task<ApplicationResponse<SendMessageResponse>> HandleAsync(
        SendConversationMessageInput request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        MessageContent? content = null;
        if (!string.IsNullOrWhiteSpace(request.Content))
        {
            var contentResult = MessageContent.Create(request.Content);
            if (contentResult.IsFailure || contentResult.Value is null)
            {
                var code = MessageContentErrorCodeResolver.Resolve(request.Content);
                return ApplicationResponse<SendMessageResponse>.Fail(
                    code,
                    contentResult.Error ?? "Message content is invalid");
            }
            content = contentResult.Value;
        }

        var access = await _conversationRepository.GetByIdWithAllParticipantsAsync(request.ConversationId, currentUserId, cancellationToken);
        if (access is null)
        {
            return ApplicationResponse<SendMessageResponse>.Fail(
                ApplicationErrorCodes.Conversation.NotFound,
                "Conversation was not found");
        }
        if (access.CallerParticipant is null)
        {
            return ApplicationResponse<SendMessageResponse>.Fail(
                ApplicationErrorCodes.Conversation.AccessDenied,
                "You do not have access to this conversation");
        }

        // Resolve and validate reply target
        MessageId? replyToMessageId = null;
        ReplyTargetSummary? replyTargetSummary = null;
        if (request.ReplyToMessageId.HasValue)
        {
            var targetMessageId = MessageId.From(request.ReplyToMessageId.Value);
            replyTargetSummary = await _messageRepository.GetReplyTargetSummaryAsync(targetMessageId, cancellationToken);
            if (replyTargetSummary is null || !replyTargetSummary.Scope.Matches(request.ConversationId))
            {
                return ApplicationResponse<SendMessageResponse>.Fail(
                    ApplicationErrorCodes.Message.NotFound,
                    "Reply target message was not found");
            }
            replyToMessageId = replyTargetSummary.MessageId;
        }

        var attachmentResolution = await _messageAttachmentResolver.ResolveAsync(
            request.AttachmentFileIds,
            currentUserId,
            cancellationToken);
        if (!attachmentResolution.Success)
        {
            return ApplicationResponse<SendMessageResponse>.Fail(
                ApplicationErrorCodes.Common.ValidationFailed,
                "Request validation failed",
                EndpointExtensions.SingleValidationError(
                    nameof(request.AttachmentFileIds),
                    ApplicationErrorCodes.Validation.Invalid,
                    attachmentResolution.Error ?? "Attachments are invalid"));
        }

        if (content is null && attachmentResolution.Attachments.Count == 0)
        {
            return ApplicationResponse<SendMessageResponse>.Fail(
                ApplicationErrorCodes.Message.ContentEmpty,
                "Message must have content or at least one attachment");
        }

        var messageResult = Message.Create(
            new MessageScope.Conversation(request.ConversationId),
            currentUserId,
            content,
            replyToMessageId);
        if (messageResult.IsFailure || messageResult.Value is null)
        {
            return ApplicationResponse<SendMessageResponse>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                messageResult.Error ?? "Unable to create conversation message");
        }

        var attachments = new List<MessageAttachment>(attachmentResolution.Attachments.Count);
        for (var i = 0; i < attachmentResolution.Attachments.Count; i++)
        {
            var resolved = attachmentResolution.Attachments[i];
            var attachmentResult = MessageAttachment.Create(
                messageResult.Value.Id,
                resolved.FileId,
                resolved.FileName,
                resolved.ContentType,
                resolved.SizeBytes,
                position: i);
            if (attachmentResult.IsFailure || attachmentResult.Value is null)
            {
                return ApplicationResponse<SendMessageResponse>.Fail(
                    ApplicationErrorCodes.Common.DomainRuleViolation,
                    attachmentResult.Error ?? "Unable to create message attachment");
            }
            attachments.Add(attachmentResult.Value);
        }

        ConversationParticipant[] hiddenParticipants = [];
        if (access.Conversation.Type == ConversationType.Direct)
        {
            hiddenParticipants = access.AllParticipants
                .Where(p => p.HiddenAtUtc is not null)
                .ToArray();
            foreach (var p in hiddenParticipants)
                p.Unhide();
        }

        await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);
        await _conversationMessageRepository.AddAsync(messageResult.Value, cancellationToken);
        if (attachments.Count > 0)
            await _messageAttachmentRepository.AddRangeAsync(attachments, cancellationToken);
        if (hiddenParticipants.Length > 0)
            await _participantRepository.UpdateRangeAsync(hiddenParticipants, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var messageConversationId = request.ConversationId;

        ReplyPreviewDto? replyTo = null;
        if (replyTargetSummary is not null)
        {
            replyTo = new ReplyPreviewDto(
                replyTargetSummary.MessageId.Value,
                replyTargetSummary.AuthorUserId.Value,
                replyTargetSummary.AuthorDisplayName,
                replyTargetSummary.AuthorUsername,
                replyTargetSummary.Content,
                replyTargetSummary.HasAttachments,
                replyTargetSummary.IsDeleted,
                replyTargetSummary.DeletedAtUtc);
        }

        await NotifyMessageCreatedSafelyAsync(
            new ConversationMessageCreatedNotification(
                messageResult.Value.Id,
                messageConversationId,
                access.Conversation.Name,
                access.Conversation.Type.ToString(),
                messageResult.Value.AuthorUserId,
                access.CallerUsername ?? string.Empty,
                access.CallerDisplayName,
                messageResult.Value.Content?.Value,
                attachments.Select(MessageAttachmentDto.FromDomain).ToArray(),
                replyTo,
                messageResult.Value.CreatedAtUtc));

        var urls = _linkPreviewService.ParseUrls(messageResult.Value.Content?.Value);
        if (urls.Count > 0)
        {
            // TODO: Replace fire-and-forget with a domain event + dedicated background worker
            // (e.g. MessageCreatedDomainEvent -> LinkPreviewResolutionWorker via a channel/queue).
            // This avoids scoped-service lifetime issues and gives proper retry/observability.
            _ = _linkPreviewService.ResolveAndNotifyForConversationAsync(
                messageResult.Value.Id,
                messageConversationId,
                access.Conversation.Name,
                access.Conversation.Type.ToString(),
                urls,
                cancellationToken);
        }

        return ApplicationResponse<SendMessageResponse>.Ok(new SendMessageResponse(
            MessageId: messageResult.Value.Id.Value,
            ConversationId: messageConversationId.Value,
            AuthorUserId: messageResult.Value.AuthorUserId.Value,
            Content: messageResult.Value.Content?.Value,
            Attachments: attachments.Select(MessageAttachmentDto.FromDomain).ToArray(),
            ReplyTo: replyTo,
            CreatedAtUtc: messageResult.Value.CreatedAtUtc));
    }

    private async Task NotifyMessageCreatedSafelyAsync(
        ConversationMessageCreatedNotification notification)
    {
        await BestEffortNotificationHelper.TryNotifyAsync(
            token => _conversationMessageNotifier.NotifyMessageCreatedAsync(notification, token),
            NotificationTimeout,
            _logger,
            "SendConversationMessage notification failed (best-effort). MessageId={MessageId}, ConversationId={ConversationId}",
            notification.MessageId,
            notification.ConversationId);
    }
}
