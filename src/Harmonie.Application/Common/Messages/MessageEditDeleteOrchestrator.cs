using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Common.Uploads;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Messages;
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly UploadedFileCleanupService _uploadedFileCleanupService;

    public MessageEditDeleteOrchestrator(
        IMessageRepository messageRepository,
        IMessageAttachmentRepository messageAttachmentRepository,
        IUnitOfWork unitOfWork,
        UploadedFileCleanupService uploadedFileCleanupService)
    {
        _messageRepository = messageRepository;
        _messageAttachmentRepository = messageAttachmentRepository;
        _unitOfWork = unitOfWork;
        _uploadedFileCleanupService = uploadedFileCleanupService;
    }

    public async Task<ApplicationResponse<MessageEditResult>> EditAsync<TContext>(
        IMessageEditDeleteScope<TContext> scope,
        MessageScope messageScope,
        MessageId messageId,
        string rawContent,
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
        var fetched = await AuthorizeAndFetchMessageAsync(
            scope, messageScope, messageId, callerId, ct);
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

        // ── Persist ─────────────────────────────────────────────────────
        await using var transaction = await _unitOfWork.BeginAsync(ct);
        await _messageRepository.UpdateAsync(message, ct);
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

        // ── Result ──────────────────────────────────────────────────────
        return ApplicationResponse<MessageEditResult>.Ok(new MessageEditResult(
            MessageId: message.Id.Value,
            AuthorUserId: message.AuthorUserId.Value,
            Content: message.Content?.Value,
            Attachments: attachments.Select(MessageAttachmentDto.FromDomain).ToArray(),
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
        var fetched = await AuthorizeAndFetchMessageAsync(
            scope, messageScope, messageId, callerId, ct);
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
        var fetched = await AuthorizeAndFetchMessageAsync(
            scope, messageScope, messageId, callerId, ct);
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

    /// <summary>
    /// Authorizes the caller via the scope and fetches the target message,
    /// validating that it belongs to the expected scope.
    /// </summary>
    private async Task<FetchMessageResult<TContext>> AuthorizeAndFetchMessageAsync<TContext>(
        IMessageEditDeleteScope<TContext> scope,
        MessageScope messageScope,
        MessageId messageId,
        UserId callerId,
        CancellationToken ct)
        where TContext : ScopeContext
    {
        var authResult = await scope.AuthorizeAsync(callerId, ct);
        if (authResult is AuthorizationResult<TContext>.Denied denied)
            return new FetchMessageResult<TContext>.Failed(denied.Error);

        var context = ((AuthorizationResult<TContext>.Authorized)authResult).Context;

        var message = await _messageRepository.GetByIdAsync(messageId, ct);
        if (message is null || message.Scope != messageScope)
        {
            return new FetchMessageResult<TContext>.Failed(new ApplicationError(
                ApplicationErrorCodes.Message.NotFound,
                "Message was not found"));
        }

        return new FetchMessageResult<TContext>.Found(context, message);
    }
}
