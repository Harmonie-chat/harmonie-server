using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Channels.AddReaction;

public sealed record ChannelAddReactionInput(GuildChannelId ChannelId, MessageId MessageId, string Emoji);

public sealed class AddReactionHandler : IAuthenticatedHandler<ChannelAddReactionInput, bool>
{
    private static readonly TimeSpan NotificationTimeout = TimeSpan.FromSeconds(5);

    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IMessageReactionRepository _reactionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IReactionNotifier _reactionNotifier;
    private readonly ILogger<AddReactionHandler> _logger;

    public AddReactionHandler(
        IGuildChannelRepository guildChannelRepository,
        IMessageRepository messageRepository,
        IMessageReactionRepository reactionRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IReactionNotifier reactionNotifier,
        ILogger<AddReactionHandler> logger)
    {
        _guildChannelRepository = guildChannelRepository;
        _messageRepository = messageRepository;
        _reactionRepository = reactionRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _reactionNotifier = reactionNotifier;
        _logger = logger;
    }

    public async Task<ApplicationResponse<bool>> HandleAsync(
        ChannelAddReactionInput request,
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
                "Reactions can only be added in text channels");
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
                ApplicationErrorCodes.Reaction.MessageNotFound,
                "Message was not found");
        }

        await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);
        var reaction = MessageReaction.Create(request.MessageId, currentUserId, request.Emoji);
        if (reaction.IsFailure || reaction.Value is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                reaction.Error ?? "Invalid reaction");
        }

        await _reactionRepository.AddAsync(reaction.Value, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var user = await _userRepository.GetByIdAsync(currentUserId, cancellationToken);
        var username = user?.Username?.Value ?? string.Empty;
        var displayName = user?.DisplayName;

        await NotifyReactionAddedSafelyAsync(
            new ChannelReactionAddedNotification(request.MessageId, request.ChannelId, ctx.Channel.GuildId, currentUserId, username, displayName, request.Emoji));

        return ApplicationResponse<bool>.Ok(true);
    }

    private async Task NotifyReactionAddedSafelyAsync(
        ChannelReactionAddedNotification notification)
    {
        await BestEffortNotificationHelper.TryNotifyAsync(
            token => _reactionNotifier.NotifyReactionAddedToChannelAsync(notification, token),
            NotificationTimeout,
            _logger,
            "AddChannelReaction notification failed (best-effort). MessageId={MessageId}, ChannelId={ChannelId}",
            notification.MessageId,
            notification.ChannelId);
    }
}
