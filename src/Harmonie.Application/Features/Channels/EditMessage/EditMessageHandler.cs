using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Channels.EditMessage;

public sealed record EditChannelMessageInput(GuildChannelId ChannelId, MessageId MessageId, string Content);

public sealed class EditMessageHandler : IAuthenticatedHandler<EditChannelMessageInput, EditMessageResponse>
{
    private static readonly TimeSpan NotificationTimeout = TimeSpan.FromSeconds(5);

    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IMessageRepository _channelMessageRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITextChannelNotifier _textChannelNotifier;
    private readonly ILogger<EditMessageHandler> _logger;

    public EditMessageHandler(
        IGuildChannelRepository guildChannelRepository,
        IMessageRepository channelMessageRepository,
        IUnitOfWork unitOfWork,
        ITextChannelNotifier textChannelNotifier,
        ILogger<EditMessageHandler> logger)
    {
        _guildChannelRepository = guildChannelRepository;
        _channelMessageRepository = channelMessageRepository;
        _unitOfWork = unitOfWork;
        _textChannelNotifier = textChannelNotifier;
        _logger = logger;
    }

    public async Task<ApplicationResponse<EditMessageResponse>> HandleAsync(
        EditChannelMessageInput request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var contentResult = MessageContent.Create(request.Content);
        if (contentResult.IsFailure || contentResult.Value is null)
        {
            var code = MessageContentErrorCodeResolver.Resolve(request.Content);
            return ApplicationResponse<EditMessageResponse>.Fail(
                code,
                contentResult.Error ?? "Message content is invalid");
        }

        var ctx = await _guildChannelRepository.GetWithCallerRoleAsync(request.ChannelId, currentUserId, cancellationToken);
        if (ctx is null)
        {
            return ApplicationResponse<EditMessageResponse>.Fail(
                ApplicationErrorCodes.Channel.NotFound,
                "Channel was not found");
        }

        if (ctx.Channel.Type != GuildChannelType.Text)
        {
            return ApplicationResponse<EditMessageResponse>.Fail(
                ApplicationErrorCodes.Channel.NotText,
                "Messages can only be edited in text channels");
        }

        if (ctx.CallerRole is null)
        {
            return ApplicationResponse<EditMessageResponse>.Fail(
                ApplicationErrorCodes.Channel.AccessDenied,
                "You do not have access to this channel");
        }

        var message = await _channelMessageRepository.GetByIdAsync(request.MessageId, cancellationToken);
        var messageChannelId = message?.ChannelId;
        if (message is null || messageChannelId is null || messageChannelId != request.ChannelId)
        {
            return ApplicationResponse<EditMessageResponse>.Fail(
                ApplicationErrorCodes.Message.NotFound,
                "Message was not found");
        }

        if (message.AuthorUserId != currentUserId)
        {
            return ApplicationResponse<EditMessageResponse>.Fail(
                ApplicationErrorCodes.Message.EditForbidden,
                "You can only edit your own messages");
        }

        var updateResult = message.UpdateContent(contentResult.Value);
        if (updateResult.IsFailure)
        {
            return ApplicationResponse<EditMessageResponse>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                updateResult.Error ?? "Message content update failed");
        }

        await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);
        await _channelMessageRepository.UpdateAsync(message, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var updatedAtUtc = message.UpdatedAtUtc;
        if (updatedAtUtc is null)
        {
            return ApplicationResponse<EditMessageResponse>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Message edit succeeded but updated timestamp is missing");
        }

        await NotifyMessageUpdatedSafelyAsync(
            new TextChannelMessageUpdatedNotification(
                message.Id,
                messageChannelId,
                ctx.Channel.Name,
                ctx.Channel.GuildId,
                ctx.GuildName ?? string.Empty,
                message.Content?.Value,
                updatedAtUtc.Value));

        return ApplicationResponse<EditMessageResponse>.Ok(new EditMessageResponse(
            MessageId: message.Id.Value,
            ChannelId: messageChannelId.Value,
            AuthorUserId: message.AuthorUserId.Value,
            Content: message.Content?.Value,
            Attachments: message.Attachments.Select(MessageAttachmentDto.FromDomain).ToArray(),
            CreatedAtUtc: message.CreatedAtUtc,
            UpdatedAtUtc: message.UpdatedAtUtc));
    }

    private async Task NotifyMessageUpdatedSafelyAsync(
        TextChannelMessageUpdatedNotification notification)
    {
        await BestEffortNotificationHelper.TryNotifyAsync(
            token => _textChannelNotifier.NotifyMessageUpdatedAsync(notification, token),
            NotificationTimeout,
            _logger,
            "EditMessage notification failed (best-effort). MessageId={MessageId}, ChannelId={ChannelId}",
            notification.MessageId,
            notification.ChannelId);
    }
}
