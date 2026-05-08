using Harmonie.Application.Common;
using Harmonie.Application.Common.Uploads;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Uploads;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Conversations.DeleteMessageAttachment;

public sealed record DeleteConversationMessageAttachmentInput(ConversationId ConversationId, MessageId MessageId, UploadedFileId AttachmentId);

public sealed class DeleteMessageAttachmentHandler : IAuthenticatedHandler<DeleteConversationMessageAttachmentInput, bool>
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _conversationMessageRepository;
    private readonly IMessageAttachmentRepository _messageAttachmentRepository;
    private readonly UploadedFileCleanupService _uploadedFileCleanupService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeleteMessageAttachmentHandler> _logger;

    public DeleteMessageAttachmentHandler(
        IConversationRepository conversationRepository,
        IMessageRepository conversationMessageRepository,
        IMessageAttachmentRepository messageAttachmentRepository,
        UploadedFileCleanupService uploadedFileCleanupService,
        IUnitOfWork unitOfWork,
        ILogger<DeleteMessageAttachmentHandler> logger)
    {
        _conversationRepository = conversationRepository;
        _conversationMessageRepository = conversationMessageRepository;
        _messageAttachmentRepository = messageAttachmentRepository;
        _uploadedFileCleanupService = uploadedFileCleanupService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApplicationResponse<bool>> HandleAsync(
        DeleteConversationMessageAttachmentInput request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var access = await _conversationRepository.GetByIdWithParticipantCheckAsync(request.ConversationId, currentUserId, cancellationToken);
        if (access is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Conversation.NotFound,
                "Conversation was not found");
        }
        if (access.Participant is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Conversation.AccessDenied,
                "You do not have access to this conversation");
        }

        var message = await _conversationMessageRepository.GetByIdAsync(request.MessageId, cancellationToken);
        if (message is null || !message.Scope.Matches(request.ConversationId))
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Message.NotFound,
                "Message was not found");
        }

        if (message.AuthorUserId != currentUserId)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Message.DeleteForbidden,
                "You can only delete attachments from your own messages");
        }

        bool deleted;
        await using (var transaction = await _unitOfWork.BeginAsync(cancellationToken))
        {
            deleted = await _messageAttachmentRepository.DeleteAsync(message.Id, request.AttachmentId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        if (!deleted)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Message.AttachmentNotFound,
                "Attachment was not found on message");
        }

        await _uploadedFileCleanupService.DeleteIfExistsAsync(request.AttachmentId, cancellationToken);

        return ApplicationResponse<bool>.Ok(true);
    }
}
