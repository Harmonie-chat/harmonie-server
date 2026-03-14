using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Conversations.DeleteMessageAttachment;

public sealed class DeleteMessageAttachmentHandler
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _conversationMessageRepository;
    private readonly UploadedFileCleanupService _uploadedFileCleanupService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeleteMessageAttachmentHandler> _logger;

    public DeleteMessageAttachmentHandler(
        IConversationRepository conversationRepository,
        IMessageRepository conversationMessageRepository,
        UploadedFileCleanupService uploadedFileCleanupService,
        IUnitOfWork unitOfWork,
        ILogger<DeleteMessageAttachmentHandler> logger)
    {
        _conversationRepository = conversationRepository;
        _conversationMessageRepository = conversationMessageRepository;
        _uploadedFileCleanupService = uploadedFileCleanupService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApplicationResponse<bool>> HandleAsync(
        ConversationId conversationId,
        MessageId messageId,
        UploadedFileId attachmentId,
        UserId callerId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "DeleteConversationMessageAttachment started. ConversationId={ConversationId}, MessageId={MessageId}, AttachmentId={AttachmentId}, CallerId={CallerId}",
            conversationId,
            messageId,
            attachmentId,
            callerId);

        var conversation = await _conversationRepository.GetByIdAsync(conversationId, cancellationToken);
        if (conversation is null)
        {
            _logger.LogWarning(
                "DeleteConversationMessageAttachment failed because conversation was not found. ConversationId={ConversationId}",
                conversationId);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Conversation.NotFound,
                "Conversation was not found");
        }

        if (conversation.User1Id != callerId && conversation.User2Id != callerId)
        {
            _logger.LogWarning(
                "DeleteConversationMessageAttachment access denied because caller is not a participant. ConversationId={ConversationId}, CallerId={CallerId}",
                conversationId,
                callerId);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Conversation.AccessDenied,
                "You do not have access to this conversation");
        }

        var message = await _conversationMessageRepository.GetByIdAsync(messageId, cancellationToken);
        var messageConversationId = message?.ConversationId;
        if (message is null || messageConversationId is null || messageConversationId != conversationId)
        {
            _logger.LogWarning(
                "DeleteConversationMessageAttachment failed because message was not found. ConversationId={ConversationId}, MessageId={MessageId}",
                conversationId,
                messageId);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Message.NotFound,
                "Message was not found");
        }

        if (message.AuthorUserId != callerId)
        {
            _logger.LogWarning(
                "DeleteConversationMessageAttachment forbidden because caller is not the author. ConversationId={ConversationId}, MessageId={MessageId}, CallerId={CallerId}",
                conversationId,
                messageId,
                callerId);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Message.DeleteForbidden,
                "You can only delete attachments from your own messages");
        }

        var removeAttachmentResult = message.RemoveAttachment(attachmentId);
        if (removeAttachmentResult.IsFailure)
        {
            _logger.LogWarning(
                "DeleteConversationMessageAttachment failed because attachment was not found on message. ConversationId={ConversationId}, MessageId={MessageId}, AttachmentId={AttachmentId}",
                conversationId,
                messageId,
                attachmentId);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Message.AttachmentNotFound,
                removeAttachmentResult.Error ?? "Attachment was not found on message");
        }

        await using (var transaction = await _unitOfWork.BeginAsync(cancellationToken))
        {
            await _conversationMessageRepository.UpdateAsync(message, cancellationToken);
            await _conversationMessageRepository.RemoveAttachmentAsync(message.Id, attachmentId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        await _uploadedFileCleanupService.DeleteIfExistsAsync(attachmentId, cancellationToken);

        _logger.LogInformation(
            "DeleteConversationMessageAttachment succeeded. ConversationId={ConversationId}, MessageId={MessageId}, AttachmentId={AttachmentId}, CallerId={CallerId}",
            conversationId,
            messageId,
            attachmentId,
            callerId);

        return ApplicationResponse<bool>.Ok(true);
    }
}
