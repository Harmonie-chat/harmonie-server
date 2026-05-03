using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Channels.PinMessage;

public sealed record ChannelPinMessageInput(GuildChannelId ChannelId, MessageId MessageId);

public sealed class PinMessageHandler : IAuthenticatedHandler<ChannelPinMessageInput, bool>
{
    private static readonly TimeSpan NotificationTimeout = TimeSpan.FromSeconds(5);

    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IPinnedMessageRepository _pinnedMessageRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPinNotifier _pinNotifier;
    private readonly ILogger<PinMessageHandler> _logger;

    public PinMessageHandler(
        IGuildChannelRepository guildChannelRepository,
        IMessageRepository messageRepository,
        IPinnedMessageRepository pinnedMessageRepository,
        IUnitOfWork unitOfWork,
        IPinNotifier pinNotifier,
        ILogger<PinMessageHandler> logger)
    {
        _guildChannelRepository = guildChannelRepository;
        _messageRepository = messageRepository;
        _pinnedMessageRepository = pinnedMessageRepository;
        _unitOfWork = unitOfWork;
        _pinNotifier = pinNotifier;
        _logger = logger;
    }

    public async Task<ApplicationResponse<bool>> HandleAsync(
        ChannelPinMessageInput request,
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
                "Messages can only be pinned in text channels");
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
                ApplicationErrorCodes.Pin.MessageNotFound,
                "Message was not found");
        }

        var pinnedMessage = PinnedMessage.Create(request.MessageId, currentUserId);
        if (pinnedMessage.IsFailure || pinnedMessage.Value is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                pinnedMessage.Error ?? "Invalid pin");
        }

        await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);
        await _pinnedMessageRepository.AddAsync(pinnedMessage.Value, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await NotifyPinAddedSafelyAsync(
            new ChannelPinAddedNotification(
                request.MessageId,
                request.ChannelId,
                ctx.Channel.Name,
                ctx.Channel.GuildId,
                ctx.GuildName ?? string.Empty,
                currentUserId,
                ctx.CallerUsername ?? string.Empty,
                ctx.CallerDisplayName,
                pinnedMessage.Value.PinnedAtUtc));

        return ApplicationResponse<bool>.Ok(true);
    }

    private async Task NotifyPinAddedSafelyAsync(
        ChannelPinAddedNotification notification)
    {
        await BestEffortNotificationHelper.TryNotifyAsync(
            token => _pinNotifier.NotifyMessagePinnedInChannelAsync(notification, token),
            NotificationTimeout,
            _logger,
            "Channel pin notification failed (best-effort). MessageId={MessageId}, ChannelId={ChannelId}",
            notification.MessageId,
            notification.ChannelId);
    }
}
