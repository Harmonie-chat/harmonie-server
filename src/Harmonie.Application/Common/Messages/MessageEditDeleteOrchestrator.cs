using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Common.Uploads;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Uploads;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Common.Messages;

/// <summary>
/// Shared orchestrator for message edit, delete, and delete-attachment operations
/// across all scopes (channels, conversations). Extracts the duplicated logic from
/// channel and conversation handlers.
/// </summary>
public sealed class MessageEditDeleteOrchestrator
{
    private readonly IMessageRepository _messageRepository;
    private readonly IMessageAttachmentRepository _messageAttachmentRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly UploadedFileCleanupService _uploadedFileCleanupService;

    public MessageEditDeleteOrchestrator(
        IMessageRepository messageRepository,
        IMessageAttachmentRepository messageAttachmentRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        UploadedFileCleanupService uploadedFileCleanupService)
    {
        _messageRepository = messageRepository;
        _messageAttachmentRepository = messageAttachmentRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _uploadedFileCleanupService = uploadedFileCleanupService;
    }

    public async Task<ApplicationResponse<MessageEditResult>> EditAsync<TContext>(
        IMessageEditDeleteScope<TContext> scope,
        MessageScope messageScope,
        MessageId messageId,
        string rawContent,
        IReadOnlyList<Guid>? mentionedUserIds,
        UserId callerId,
        CancellationToken ct)
        where TContext : ScopeContext
    {
        // ── Content validation ──────────────────────────────────────────
        var contentResult = MessageContent.Create(rawContent);
        if (contentResult.IsFailure || contentResult.Value is null)
        {
            var code = MessageContentErrorCodeResolver.Resolve(rawContent);
            return ApplicationResponse<MessageEditResult>.Fail(
                code,
                contentResult.Error ?? "Message content is invalid");
        }

        // ── Authorization + message fetch ───────────────────────────────
        var fetched = await MessageScopeAuthorizer.AuthorizeAndFetchAsync(
            _messageRepository,
            (caller, ct) => scope.AuthorizeAsync(caller, ct),
            messageScope, messageId, callerId,
            ApplicationErrorCodes.Message.NotFound, ct);
        if (fetched is FetchMessageResult<TContext>.Failed failed)
            return ApplicationResponse<MessageEditResult>.Fail(failed.Error);

        var (context, message) = (FetchMessageResult<TContext>.Found)fetched;

        // ── Author check ────────────────────────────────────────────────
        if (message.AuthorUserId != callerId)
        {
            return ApplicationResponse<MessageEditResult>.Fail(
                ApplicationErrorCodes.Message.EditForbidden,
                "You can only edit your own messages");
        }

        // ── Update content ──────────────────────────────────────────────
        var updateResult = message.UpdateContent(contentResult.Value);
        if (updateResult.IsFailure)
        {
            return ApplicationResponse<MessageEditResult>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                updateResult.Error ?? "Message content update failed");
        }

        // ── Updated timestamp validation (before commit: UpdateContent always sets it,
        //     this guard is defensive against future domain changes) ─────
        var updatedAtUtc = message.UpdatedAtUtc;
        if (updatedAtUtc is null)
        {
            return ApplicationResponse<MessageEditResult>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Message edit succeeded but updated timestamp is missing");
        }

        // ── Mention validation ──────────────────────────────────────────
        IReadOnlyList<Guid>? validatedMentionIds = null;
        if (mentionedUserIds is { Count: > 0 })
        {
            var distinctMentionIds = mentionedUserIds.Distinct().ToArray();
            var userIds = distinctMentionIds.Select(UserId.From).ToArray();

            // Verify all users exist
            var existingUsers = await _userRepository.GetManyByIdsAsync(userIds.ToArray(), ct);
            var existingUserIds = existingUsers.Select(u => u.Id).ToHashSet();
            var missingIds = userIds.Where(id => !existingUserIds.Contains(id)).ToArray();
            if (missingIds.Length > 0)
            {
                return ApplicationResponse<MessageEditResult>.Fail(
                    ApplicationErrorCodes.Message.MentionedUserNotFound,
                    $"One or more mentioned users were not found: {string.Join(", ", missingIds.Select(id => id.Value))}");
            }

            // Verify membership via scope
            var validateResult = await scope.ValidateMentionedUsersAsync(userIds.ToArray(), context, ct);
            if (validateResult.IsFailure)
            {
                return ApplicationResponse<MessageEditResult>.Fail(
                    ApplicationErrorCodes.Message.MentionedUserNotMember,
                    validateResult.Error ?? "One or more mentioned users are not members of this scope");
            }

            validatedMentionIds = distinctMentionIds;
        }

        // ── Persist ─────────────────────────────────────────────────────
        await using var transaction = await _unitOfWork.BeginAsync(ct);
        await _messageRepository.UpdateAsync(message, ct);
        if (validatedMentionIds is not null)
            await _messageRepository.ReplaceMentionsAsync(message.Id, validatedMentionIds.Select(UserId.From).ToArray(), ct);
        await transaction.CommitAsync(ct);

        // ── Notify ──────────────────────────────────────────────────────
        await scope.NotifyMessageUpdatedAsync(
            context,
            message.Id,
            message.Content?.Value,
            updatedAtUtc.Value,
            ct);

        // ── Attachments for response ────────────────────────────────────
        var attachments = await _messageAttachmentRepository.GetByMessageIdAsync(messageId, ct);

        // ── Retrieve mentions for response ──────────────────────────────
        IReadOnlyList<Guid> mentionIdsResponse;
        if (validatedMentionIds is not null)
        {
            mentionIdsResponse = validatedMentionIds;
        }
        else
        {
            var mentionsDict = await _messageRepository.GetMentionedUserIdsByMessageIdAsync(
                [message.Id.Value], ct);
            mentionIdsResponse = mentionsDict.TryGetValue(message.Id.Value, out var ids) ? ids : Array.Empty<Guid>();
        }

        // ── Result ──────────────────────────────────────────────────────
        return ApplicationResponse<MessageEditResult>.Ok(new MessageEditResult(
            MessageId: message.Id.Value,
            AuthorUserId: message.AuthorUserId.Value,
            Content: message.Content?.Value,
            Attachments: attachments.Select(MessageAttachmentDto.FromDomain).ToArray(),
            MentionedUserIds: mentionIdsResponse.ToArray(),
            CreatedAtUtc: message.CreatedAtUtc,
            UpdatedAtUtc: updatedAtUtc));
    }

    public async Task<ApplicationResponse<bool>> DeleteAsync<TContext>(
        IMessageEditDeleteScope<TContext> scope,
        MessageScope messageScope,
        MessageId messageId,
        UserId callerId,
        CancellationToken ct)
        where TContext : ScopeContext
    {
        // ── Authorization + message fetch ───────────────────────────────
        var fetched = await MessageScopeAuthorizer.AuthorizeAndFetchAsync(
            _messageRepository,
            (caller, ct) => scope.AuthorizeAsync(caller, ct),
            messageScope, messageId, callerId,
            ApplicationErrorCodes.Message.NotFound, ct);
        if (fetched is FetchMessageResult<TContext>.Failed failed)
            return ApplicationResponse<bool>.Fail(failed.Error);

        var (context, message) = (FetchMessageResult<TContext>.Found)fetched;

        // ── Author check (with scope-specific admin override) ───────────
        // Channel scopes allow admins to delete others' messages (CanDeleteOthersMessages = true).
        // Conversation scopes never allow non-authors to delete (CanDeleteOthersMessages = false).
        if (message.AuthorUserId != callerId && !scope.CanDeleteOthersMessages(context))
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Message.DeleteForbidden,
                "You can only delete your own messages");
        }

        // ── Soft delete ─────────────────────────────────────────────────
        var deleteResult = message.Delete();
        if (deleteResult.IsFailure)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                deleteResult.Error ?? "Message deletion failed");
        }

        // ── Persist ─────────────────────────────────────────────────────
        await using var transaction = await _unitOfWork.BeginAsync(ct);
        await _messageRepository.SoftDeleteAsync(message, ct);
        await transaction.CommitAsync(ct);

        // ── Notify ──────────────────────────────────────────────────────
        await scope.NotifyMessageDeletedAsync(context, message.Id, ct);

        return ApplicationResponse<bool>.Ok(true);
    }

    public async Task<ApplicationResponse<bool>> DeleteAttachmentAsync<TContext>(
        IMessageEditDeleteScope<TContext> scope,
        MessageScope messageScope,
        MessageId messageId,
        UploadedFileId attachmentId,
        UserId callerId,
        CancellationToken ct)
        where TContext : ScopeContext
    {
        // ── Authorization + message fetch ───────────────────────────────
        var fetched = await MessageScopeAuthorizer.AuthorizeAndFetchAsync(
            _messageRepository,
            (caller, ct) => scope.AuthorizeAsync(caller, ct),
            messageScope, messageId, callerId,
            ApplicationErrorCodes.Message.NotFound, ct);
        if (fetched is FetchMessageResult<TContext>.Failed failed)
            return ApplicationResponse<bool>.Fail(failed.Error);

        var (_, message) = (FetchMessageResult<TContext>.Found)fetched;

        // ── Author check ────────────────────────────────────────────────
        if (message.AuthorUserId != callerId)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Message.DeleteForbidden,
                "You can only delete attachments from your own messages");
        }

        // ── Delete attachment + persist ─────────────────────────────────
        bool deleted;
        await using (var transaction = await _unitOfWork.BeginAsync(ct))
        {
            deleted = await _messageAttachmentRepository.DeleteAsync(messageId, attachmentId, ct);
            await transaction.CommitAsync(ct);
        }

        if (!deleted)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Message.AttachmentNotFound,
                "Attachment was not found on message");
        }

        // ── Clean up uploaded file ──────────────────────────────────────
        await _uploadedFileCleanupService.DeleteIfExistsAsync(attachmentId, ct);

        return ApplicationResponse<bool>.Ok(true);
    }
}
