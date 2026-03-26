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
        if (!access.IsParticipant)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Conversation.AccessDenied,
                "You do not have access to this conversation");
        }

        var message = await _conversationMessageRepository.GetByIdAsync(request.MessageId, cancellationToken);
        var messageConversationId = message?.ConversationId;
        if (message is null || messageConversationId is null || messageConversationId != request.ConversationId)
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

        var removeAttachmentResult = message.RemoveAttachment(request.AttachmentId);
        if (removeAttachmentResult.IsFailure)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Message.AttachmentNotFound,
                removeAttachmentResult.Error ?? "Attachment was not found on message");
        }

        await using (var transaction = await _unitOfWork.BeginAsync(cancellationToken))
        {
            await _conversationMessageRepository.UpdateAsync(message, cancellationToken);
            await _conversationMessageRepository.RemoveAttachmentAsync(message.Id, request.AttachmentId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        await _uploadedFileCleanupService.DeleteIfExistsAsync(request.AttachmentId, cancellationToken);

        return ApplicationResponse<bool>.Ok(true);
    }
}
