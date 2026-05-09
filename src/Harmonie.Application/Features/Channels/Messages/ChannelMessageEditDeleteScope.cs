using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Domain.Common;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Channels.Messages;

/// <summary>
/// Channel-specific implementation of <see cref="IMessageEditDeleteScope{TContext}"/>.
/// </summary>
public sealed class ChannelMessageEditDeleteScope : IMessageEditDeleteScope<ChannelMessageEditDeleteScope.Context>
{
    private static readonly TimeSpan NotificationTimeout = TimeSpan.FromSeconds(5);

    public sealed record Context(
        GuildChannelId ChannelId,
        string ChannelName,
        GuildId GuildId,
        string GuildName,
        GuildRole? CallerRole) : ScopeContext;

    private readonly GuildChannelId _channelId;
    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IGuildMemberRepository _guildMemberRepository;
    private readonly ITextChannelNotifier _textChannelNotifier;
    private readonly ILogger<ChannelMessageEditDeleteScope> _logger;

    public ChannelMessageEditDeleteScope(
        GuildChannelId channelId,
        IGuildChannelRepository guildChannelRepository,
        IGuildMemberRepository guildMemberRepository,
        ITextChannelNotifier textChannelNotifier,
        ILogger<ChannelMessageEditDeleteScope> logger)
    {
        _channelId = channelId;
        _guildChannelRepository = guildChannelRepository;
        _guildMemberRepository = guildMemberRepository;
        _textChannelNotifier = textChannelNotifier;
        _logger = logger;
    }

    public async Task<AuthorizationResult<Context>> AuthorizeAsync(UserId caller, CancellationToken ct)
    {
        var result = await ChannelScopeAuthorizer.AuthorizeAsync(_guildChannelRepository, _channelId, caller, ct);
        if (result is ChannelAuthResult.Denied denied)
            return new AuthorizationResult<Context>.Denied(denied.Error);

        var access = ((ChannelAuthResult.Authorized)result).Context;
        return new AuthorizationResult<Context>.Authorized(new Context(
            _channelId,
            access.Channel.Name,
            access.Channel.GuildId,
            access.GuildName ?? string.Empty,
            access.CallerRole));
    }

    // In channels, admins can delete any message, not just their own.
    public bool CanDeleteOthersMessages(Context context)
        => context.CallerRole == GuildRole.Admin;

    public async Task<Result> ValidateMentionedUsersAsync(
        IReadOnlyCollection<UserId> userIds,
        Context context,
        CancellationToken ct)
    {
        foreach (var userId in userIds)
        {
            var isMember = await _guildMemberRepository.IsMemberAsync(context.GuildId, userId, ct);
            if (!isMember)
            {
                return Result.Failure($"User {userId.Value} is not a member of guild {context.GuildId.Value}");
            }
        }

        return Result.Success();
    }

    public async Task NotifyMessageUpdatedAsync(
        Context context,
        MessageId messageId,
        string? content,
        DateTime updatedAtUtc,
        CancellationToken ct)
    {
        var notification = new TextChannelMessageUpdatedNotification(
            messageId,
            context.ChannelId,
            context.ChannelName,
            context.GuildId,
            context.GuildName,
            content,
            updatedAtUtc);

        await BestEffortNotificationHelper.TryNotifyAsync(
            token => _textChannelNotifier.NotifyMessageUpdatedAsync(notification, token),
            NotificationTimeout,
            _logger,
            "EditMessage notification failed (best-effort). MessageId={MessageId}, ChannelId={ChannelId}",
            notification.MessageId,
            notification.ChannelId);
    }

    public async Task NotifyMessageDeletedAsync(
        Context context,
        MessageId messageId,
        CancellationToken ct)
    {
        var notification = new TextChannelMessageDeletedNotification(
            messageId,
            context.ChannelId,
            context.ChannelName,
            context.GuildId,
            context.GuildName);

        await BestEffortNotificationHelper.TryNotifyAsync(
            token => _textChannelNotifier.NotifyMessageDeletedAsync(notification, token),
            NotificationTimeout,
            _logger,
            "DeleteMessage notification failed (best-effort). MessageId={MessageId}, ChannelId={ChannelId}",
            notification.MessageId,
            notification.ChannelId);
    }
}
