using Harmonie.Application.Common;
using Harmonie.Application.Common.Uploads;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Uploads;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Channels.DeleteMessageAttachment;

public sealed record DeleteChannelMessageAttachmentInput(GuildChannelId ChannelId, MessageId MessageId, UploadedFileId AttachmentId);

public sealed class DeleteMessageAttachmentHandler : IAuthenticatedHandler<DeleteChannelMessageAttachmentInput, bool>
{
    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly UploadedFileCleanupService _uploadedFileCleanupService;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteMessageAttachmentHandler(
        IGuildChannelRepository guildChannelRepository,
        IMessageRepository messageRepository,
        UploadedFileCleanupService uploadedFileCleanupService,
        IUnitOfWork unitOfWork)
    {
        _guildChannelRepository = guildChannelRepository;
        _messageRepository = messageRepository;
        _uploadedFileCleanupService = uploadedFileCleanupService;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResponse<bool>> HandleAsync(
        DeleteChannelMessageAttachmentInput request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var ctx = await _guildChannelRepository.GetWithCallerRoleAsync(request.ChannelId, currentUserId, cancellationToken);
        if (ctx is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Channel.NotFound,
                "Channel was not found");
        }

        if (ctx.Channel.Type != GuildChannelType.Text)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Channel.NotText,
                "Attachments can only be deleted from messages in text channels");
        }

        if (ctx.CallerRole is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Channel.AccessDenied,
                "You do not have access to this channel");
        }

        var message = await _messageRepository.GetByIdAsync(request.MessageId, cancellationToken);
        var messageChannelId = message?.ChannelId;
        if (message is null || messageChannelId is null || messageChannelId != request.ChannelId)
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
            await _messageRepository.UpdateAsync(message, cancellationToken);
            await _messageRepository.RemoveAttachmentAsync(message.Id, request.AttachmentId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        await _uploadedFileCleanupService.DeleteIfExistsAsync(request.AttachmentId, cancellationToken);

        return ApplicationResponse<bool>.Ok(true);
    }
}
