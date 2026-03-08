using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Channels.SendMessage;

public sealed class SendMessageHandler
{
    private static readonly TimeSpan NotificationTimeout = TimeSpan.FromSeconds(5);

    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IChannelMessageRepository _channelMessageRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITextChannelNotifier _textChannelNotifier;
    private readonly ILogger<SendMessageHandler> _logger;

    public SendMessageHandler(
        IGuildChannelRepository guildChannelRepository,
        IChannelMessageRepository channelMessageRepository,
        IUnitOfWork unitOfWork,
        ITextChannelNotifier textChannelNotifier,
        ILogger<SendMessageHandler> logger)
    {
        _guildChannelRepository = guildChannelRepository;
        _channelMessageRepository = channelMessageRepository;
        _unitOfWork = unitOfWork;
        _textChannelNotifier = textChannelNotifier;
        _logger = logger;
    }

    public async Task<ApplicationResponse<SendMessageResponse>> HandleAsync(
        GuildChannelId channelId,
        SendMessageRequest request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "SendMessage started. ChannelId={ChannelId}, UserId={UserId}",
            channelId,
            currentUserId);

        var contentResult = MessageContent.Create(request.Content);
        if (contentResult.IsFailure || contentResult.Value is null)
        {
            _logger.LogWarning(
                "SendMessage validation failed. ChannelId={ChannelId}, UserId={UserId}, Error={Error}",
                channelId,
                currentUserId,
                contentResult.Error);

            var code = ResolveContentErrorCode(request.Content);
            return ApplicationResponse<SendMessageResponse>.Fail(
                code,
                contentResult.Error ?? "Message content is invalid");
        }

        var ctx = await _guildChannelRepository.GetWithCallerRoleAsync(channelId, currentUserId, cancellationToken);
        if (ctx is null)
        {
            _logger.LogWarning(
                "SendMessage failed because channel was not found. ChannelId={ChannelId}, UserId={UserId}",
                channelId,
                currentUserId);

            return ApplicationResponse<SendMessageResponse>.Fail(
                ApplicationErrorCodes.Channel.NotFound,
                "Channel was not found");
        }

        if (ctx.Channel.Type != GuildChannelType.Text)
        {
            _logger.LogWarning(
                "SendMessage failed because channel is not text. ChannelId={ChannelId}, ChannelType={ChannelType}, UserId={UserId}",
                channelId,
                ctx.Channel.Type,
                currentUserId);

            return ApplicationResponse<SendMessageResponse>.Fail(
                ApplicationErrorCodes.Channel.NotText,
                "Messages can only be sent to text channels");
        }

        if (ctx.CallerRole is null)
        {
            _logger.LogWarning(
                "SendMessage access denied. ChannelId={ChannelId}, GuildId={GuildId}, UserId={UserId}",
                channelId,
                ctx.Channel.GuildId,
                currentUserId);

            return ApplicationResponse<SendMessageResponse>.Fail(
                ApplicationErrorCodes.Channel.AccessDenied,
                "You do not have access to this channel");
        }

        var messageResult = ChannelMessage.Create(
            channelId,
            currentUserId,
            contentResult.Value);
        if (messageResult.IsFailure || messageResult.Value is null)
        {
            _logger.LogWarning(
                "SendMessage domain creation failed. ChannelId={ChannelId}, UserId={UserId}, Error={Error}",
                channelId,
                currentUserId,
                messageResult.Error);

            return ApplicationResponse<SendMessageResponse>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                messageResult.Error ?? "Unable to create channel message");
        }

        await using (var transaction = await _unitOfWork.BeginAsync(cancellationToken))
        {
            await _channelMessageRepository.AddAsync(messageResult.Value, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        await NotifyMessageCreatedSafelyAsync(
            new TextChannelMessageCreatedNotification(
                messageResult.Value.Id,
                messageResult.Value.ChannelId,
                messageResult.Value.AuthorUserId,
                messageResult.Value.Content.Value,
                messageResult.Value.CreatedAtUtc));

        _logger.LogInformation(
            "SendMessage succeeded. MessageId={MessageId}, ChannelId={ChannelId}, UserId={UserId}",
            messageResult.Value.Id,
            messageResult.Value.ChannelId,
            messageResult.Value.AuthorUserId);

        var payload = new SendMessageResponse(
            MessageId: messageResult.Value.Id.ToString(),
            ChannelId: messageResult.Value.ChannelId.ToString(),
            AuthorUserId: messageResult.Value.AuthorUserId.ToString(),
            Content: messageResult.Value.Content.Value,
            CreatedAtUtc: messageResult.Value.CreatedAtUtc);

        return ApplicationResponse<SendMessageResponse>.Ok(payload);
    }

    private static string ResolveContentErrorCode(string? rawContent)
    {
        if (rawContent is null || rawContent.Trim().Length == 0)
            return ApplicationErrorCodes.Message.ContentEmpty;

        return rawContent.Trim().Length > MessageContent.MaxLength
            ? ApplicationErrorCodes.Message.ContentTooLong
            : ApplicationErrorCodes.Common.DomainRuleViolation;
    }

    private async Task NotifyMessageCreatedSafelyAsync(
        TextChannelMessageCreatedNotification notification)
    {
        try
        {
            using var notificationCts = new CancellationTokenSource(NotificationTimeout);
            await _textChannelNotifier.NotifyMessageCreatedAsync(notification, notificationCts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "SendMessage notification failed (best-effort). MessageId={MessageId}, ChannelId={ChannelId}",
                notification.MessageId,
                notification.ChannelId);
        }
    }
}
