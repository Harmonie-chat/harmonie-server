using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Application.Services;
using Harmonie.Domain.Common;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Common.Messages;

/// <summary>
/// Shared orchestrator for sending messages across all scopes (channels, conversations).
/// Extracts the identical logic that was duplicated between channel and conversation
/// SendMessage handlers.
/// </summary>
public sealed class MessageSendOrchestrator
{
    private readonly IMessageRepository _messageRepository;
    private readonly IMessageAttachmentRepository _messageAttachmentRepository;
    private readonly MessageAttachmentResolver _attachmentResolver;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public MessageSendOrchestrator(
        IMessageRepository messageRepository,
        IMessageAttachmentRepository messageAttachmentRepository,
        MessageAttachmentResolver attachmentResolver,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _messageRepository = messageRepository;
        _messageAttachmentRepository = messageAttachmentRepository;
        _attachmentResolver = attachmentResolver;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    /// <remarks>
    /// Mention membership is validated before the transaction opens.
    /// There is a theoretical race window: if a user is removed from the guild/conversation
    /// between validation and commit, a mention to a non-member may be persisted.
    /// This is accepted as the window is narrow and the impact is non-blocking
    /// (the mention is still a valid FK reference).
    /// </remarks>
    public async Task<ApplicationResponse<MessageSendResult>> SendAsync<TContext>(
        ISendMessageScope<TContext> scope,
        MessageScope messageScope,
        string? rawContent,
        IReadOnlyList<Guid>? attachmentFileIds,
        Guid? replyToMessageId,
        IReadOnlyList<Guid>? mentionedUserIds,
        UserId callerId,
        CancellationToken ct)
        where TContext : ScopeContext
    {
        // ── Content validation ──────────────────────────────────────────
        MessageContent? content = null;
        if (!string.IsNullOrWhiteSpace(rawContent))
        {
            var contentResult = MessageContent.Create(rawContent);
            if (contentResult.IsFailure || contentResult.Value is null)
            {
                var code = MessageContentErrorCodeResolver.Resolve(rawContent);
                return ApplicationResponse<MessageSendResult>.Fail(
                    code,
                    contentResult.Error ?? "Message content is invalid");
            }
            content = contentResult.Value;
        }

        // ── Authorization ───────────────────────────────────────────────
        var authResult = await scope.AuthorizeAsync(callerId, ct);
        if (authResult is AuthorizationResult<TContext>.Denied denied)
        {
            return ApplicationResponse<MessageSendResult>.Fail(denied.Error);
        }

        var context = ((AuthorizationResult<TContext>.Authorized)authResult).Context;

        // ── Reply target resolution ─────────────────────────────────────
        MessageId? replyToTargetId = null;
        ReplyTargetSummary? replyTargetSummary = null;
        if (replyToMessageId.HasValue)
        {
            var targetMessageId = MessageId.From(replyToMessageId.Value);
            replyTargetSummary = await _messageRepository.GetReplyTargetSummaryAsync(targetMessageId, ct);
            if (replyTargetSummary is null || replyTargetSummary.Scope != messageScope)
            {
                return ApplicationResponse<MessageSendResult>.Fail(
                    ApplicationErrorCodes.Message.NotFound,
                    "Reply target message was not found");
            }
            replyToTargetId = replyTargetSummary.MessageId;
        }

        // ── Attachment resolution ───────────────────────────────────────
        var attachmentResolution = await _attachmentResolver.ResolveAsync(
            attachmentFileIds,
            callerId,
            ct);
        if (!attachmentResolution.Success)
        {
            return ApplicationResponse<MessageSendResult>.Fail(
                ApplicationErrorCodes.Common.ValidationFailed,
                "Request validation failed",
                EndpointExtensions.SingleValidationError(
                    nameof(attachmentFileIds),
                    ApplicationErrorCodes.Validation.Invalid,
                    attachmentResolution.Error ?? "Attachments are invalid"));
        }

        // ── Content + attachments empty check ───────────────────────────
        if (content is null && attachmentResolution.Attachments.Count == 0)
        {
            return ApplicationResponse<MessageSendResult>.Fail(
                ApplicationErrorCodes.Message.ContentEmpty,
                "Message must have content or at least one attachment");
        }

        // ── Mention validation ──────────────────────────────────────────
        UserId[]? mentionUserIds = null;
        if (mentionedUserIds is { Count: > 0 })
        {
            var validated = await MentionValidationHelper.ValidateAsync(
                mentionedUserIds,
                _userRepository,
                (ids, ctx, t) => scope.ValidateMentionedUsersAsync(ids, ctx, t),
                context,
                ct);

            if (validated is MentionValidationResult.Failure failure)
                return ApplicationResponse<MessageSendResult>.Fail(failure.ErrorCode, failure.ErrorMessage);

            mentionUserIds = ((MentionValidationResult.Success)validated).Value;
        }

        // ── Domain message creation ─────────────────────────────────────
        var messageResult = Message.Create(
            messageScope,
            callerId,
            content,
            replyToTargetId,
            mentionUserIds);
        if (messageResult.IsFailure || messageResult.Value is null)
        {
            return ApplicationResponse<MessageSendResult>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                messageResult.Error ?? "Unable to create message");
        }

        // ── Domain attachment creation ──────────────────────────────────
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
                return ApplicationResponse<MessageSendResult>.Fail(
                    ApplicationErrorCodes.Common.DomainRuleViolation,
                    attachmentResult.Error ?? "Unable to create message attachment");
            }
            attachments.Add(attachmentResult.Value);
        }

        // ── Persist ─────────────────────────────────────────────────────
        await using var transaction = await _unitOfWork.BeginAsync(ct);
        await _messageRepository.AddAsync(messageResult.Value, ct);
        if (attachments.Count > 0)
            await _messageAttachmentRepository.AddRangeAsync(attachments, ct);
        if (mentionUserIds is { Length: > 0 })
            await _messageRepository.AddMentionsAsync(messageResult.Value.Id, mentionUserIds, ct);
        await scope.ApplyInTransactionSideEffectsAsync(context, ct);
        await transaction.CommitAsync(ct);

        // ── Reply preview DTO ───────────────────────────────────────────
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

        // ── Attachment DTO mapping ──────────────────────────────────────
        var attachmentDtos = attachments.Select(MessageAttachmentDto.FromDomain).ToArray();

        // ── Notify ──────────────────────────────────────────────────────
        await scope.NotifyMessageCreatedAsync(
            context,
            messageResult.Value,
            attachmentDtos,
            replyTo,
            ct);

        // ── Link previews (fire-and-forget) ─────────────────────────────
        var urls = LinkPreviewResolutionService.ParseUrls(messageResult.Value.Content?.Value);
        if (urls.Count > 0)
        {
            scope.ScheduleLinkPreviewResolution(context, messageResult.Value, urls, ct);
        }

        // ── Mention DTO mapping ────────────────────────────────────────
        var mentionDtos = mentionUserIds?.Select(id => id.Value).ToArray() ?? Array.Empty<Guid>();

        // ── Result ──────────────────────────────────────────────────────
        return ApplicationResponse<MessageSendResult>.Ok(new MessageSendResult(
            MessageId: messageResult.Value.Id.Value,
            AuthorUserId: messageResult.Value.AuthorUserId.Value,
            Content: messageResult.Value.Content?.Value,
            Attachments: attachmentDtos,
            ReplyTo: replyTo,
            MentionedUserIds: mentionDtos,
            CreatedAtUtc: messageResult.Value.CreatedAtUtc));
    }
}
