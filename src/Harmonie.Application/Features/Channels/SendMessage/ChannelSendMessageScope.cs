using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Application.Services;
using Harmonie.Domain.Common;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Channels.SendMessage;

/// <summary>
/// Channel-specific implementation of <see cref="ISendMessageScope{TContext}"/>.
/// </summary>
public sealed class ChannelSendMessageScope : ISendMessageScope<ChannelSendMessageScope.Context>
{
    private static readonly TimeSpan NotificationTimeout = TimeSpan.FromSeconds(5);

    public sealed record Context(
        GuildChannelId ChannelId,
        string ChannelName,
        GuildId GuildId,
        string GuildName,
        string CallerUsername,
        string CallerDisplayName) : ScopeContext;

    private readonly GuildChannelId _channelId;
    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IGuildMemberRepository _guildMemberRepository;
    private readonly ITextChannelNotifier _textChannelNotifier;
    private readonly LinkPreviewResolutionService _linkPreviewService;
    private readonly ILogger<ChannelSendMessageScope> _logger;

    public ChannelSendMessageScope(
        GuildChannelId channelId,
        IGuildChannelRepository guildChannelRepository,
        IGuildMemberRepository guildMemberRepository,
        ITextChannelNotifier textChannelNotifier,
        LinkPreviewResolutionService linkPreviewService,
        ILogger<ChannelSendMessageScope> logger)
    {
        _channelId = channelId;
        _guildChannelRepository = guildChannelRepository;
        _guildMemberRepository = guildMemberRepository;
        _textChannelNotifier = textChannelNotifier;
        _linkPreviewService = linkPreviewService;
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
            access.CallerUsername ?? string.Empty,
            access.CallerDisplayName ?? string.Empty));
    }

    public Task ApplyInTransactionSideEffectsAsync(Context context, CancellationToken ct)
        => Task.CompletedTask;

    public async Task<Result> ValidateMentionedUsersAsync(
        IReadOnlyCollection<UserId> userIds,
        Context context,
        CancellationToken ct)
    {
        if (userIds.Count == 0)
            return Result.Success();

        var memberSet = await _guildMemberRepository.GetMembersInAsync(context.GuildId, userIds.ToArray(), ct);
        var nonMembers = userIds.Where(id => !memberSet.Contains(id)).ToArray();
        if (nonMembers.Length > 0)
        {
            return Result.Failure($"Users not members of guild {context.GuildId.Value}: {string.Join(", ", nonMembers.Select(id => id.Value))}");
        }

        return Result.Success();
    }

    public async Task NotifyMessageCreatedAsync(
        Context context,
        Message message,
        IReadOnlyList<MessageAttachmentDto> attachments,
        ReplyPreviewDto? replyTo,
        CancellationToken ct)
    {
        var notification = new TextChannelMessageCreatedNotification(
            message.Id,
            context.ChannelId,
            context.ChannelName,
            context.GuildId,
            context.GuildName,
            message.AuthorUserId,
            context.CallerUsername,
            context.CallerDisplayName,
            message.Content?.Value,
            attachments,
            replyTo,
            message.MentionedUserIds.Select(id => id.Value).ToArray(),
            message.CreatedAtUtc);

        await BestEffortNotificationHelper.TryNotifyAsync(
            token => _textChannelNotifier.NotifyMessageCreatedAsync(notification, token),
            NotificationTimeout,
            _logger,
            "SendMessage notification failed (best-effort). MessageId={MessageId}, ChannelId={ChannelId}",
            notification.MessageId,
            notification.ChannelId);
    }

    public void ScheduleLinkPreviewResolution(
        Context context,
        Message message,
        IReadOnlyList<Uri> urls,
        CancellationToken ct)
    {
        // TODO: Replace fire-and-forget with a domain event + dedicated background worker
        _ = _linkPreviewService.ResolveAndNotifyForChannelAsync(
            message.Id,
            context.ChannelId,
            context.ChannelName,
            context.GuildId,
            context.GuildName,
            urls,
            ct);
    }
}
