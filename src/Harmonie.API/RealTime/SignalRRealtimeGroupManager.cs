using Harmonie.Application.Interfaces;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Microsoft.AspNetCore.SignalR;

namespace Harmonie.API.RealTime;

public sealed class SignalRRealtimeGroupManager : IRealtimeGroupManager
{
    private readonly IHubContext<RealtimeHub> _hubContext;
    private readonly IConnectionTracker _connectionTracker;
    private readonly IUserSubscriptionRepository _userSubscriptionRepository;
    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IGuildMemberRepository _guildMemberRepository;

    public SignalRRealtimeGroupManager(
        IHubContext<RealtimeHub> hubContext,
        IConnectionTracker connectionTracker,
        IUserSubscriptionRepository userSubscriptionRepository,
        IGuildChannelRepository guildChannelRepository,
        IGuildMemberRepository guildMemberRepository)
    {
        _hubContext = hubContext;
        _connectionTracker = connectionTracker;
        _userSubscriptionRepository = userSubscriptionRepository;
        _guildChannelRepository = guildChannelRepository;
        _guildMemberRepository = guildMemberRepository;
    }

    public async Task SubscribeConnectionAsync(
        UserId userId,
        string connectionId,
        CancellationToken cancellationToken = default)
    {
        var subscriptions = await _userSubscriptionRepository.GetAllAsync(userId, cancellationToken);

        var tasks = new List<Task>(
            subscriptions.GuildIds.Count
            + subscriptions.TextChannelIds.Count
            + subscriptions.ConversationIds.Count);

        foreach (var guildId in subscriptions.GuildIds)
        {
            tasks.Add(_hubContext.Groups.AddToGroupAsync(
                connectionId,
                RealtimeHub.GetGuildGroupName(guildId),
                cancellationToken));
        }

        foreach (var channelId in subscriptions.TextChannelIds)
        {
            tasks.Add(_hubContext.Groups.AddToGroupAsync(
                connectionId,
                RealtimeHub.GetChannelGroupName(channelId),
                cancellationToken));
        }

        foreach (var conversationId in subscriptions.ConversationIds)
        {
            tasks.Add(_hubContext.Groups.AddToGroupAsync(
                connectionId,
                RealtimeHub.GetConversationGroupName(conversationId),
                cancellationToken));
        }

        await Task.WhenAll(tasks);
    }

    public async Task AddUserToGuildGroupsAsync(
        UserId userId,
        GuildId guildId,
        CancellationToken cancellationToken = default)
    {
        var connectionIds = _connectionTracker.GetConnectionIds(userId);
        if (connectionIds.Count == 0)
            return;

        var channels = await _guildChannelRepository.GetByGuildIdAsync(guildId, cancellationToken);
        var textChannels = channels.Where(c => c.Type == GuildChannelType.Text).ToList();

        var tasks = new List<Task>(connectionIds.Count * (1 + textChannels.Count));

        foreach (var connectionId in connectionIds)
        {
            tasks.Add(_hubContext.Groups.AddToGroupAsync(
                connectionId,
                RealtimeHub.GetGuildGroupName(guildId),
                cancellationToken));

            foreach (var channel in textChannels)
            {
                tasks.Add(_hubContext.Groups.AddToGroupAsync(
                    connectionId,
                    RealtimeHub.GetChannelGroupName(channel.Id),
                    cancellationToken));
            }
        }

        await Task.WhenAll(tasks);
    }

    public async Task RemoveUserFromGuildGroupsAsync(
        UserId userId,
        GuildId guildId,
        CancellationToken cancellationToken = default)
    {
        var connectionIds = _connectionTracker.GetConnectionIds(userId);
        if (connectionIds.Count == 0)
            return;

        var channels = await _guildChannelRepository.GetByGuildIdAsync(guildId, cancellationToken);
        var textChannels = channels.Where(c => c.Type == GuildChannelType.Text).ToList();

        var tasks = new List<Task>(connectionIds.Count * (1 + textChannels.Count));

        foreach (var connectionId in connectionIds)
        {
            tasks.Add(_hubContext.Groups.RemoveFromGroupAsync(
                connectionId,
                RealtimeHub.GetGuildGroupName(guildId),
                cancellationToken));

            foreach (var channel in textChannels)
            {
                tasks.Add(_hubContext.Groups.RemoveFromGroupAsync(
                    connectionId,
                    RealtimeHub.GetChannelGroupName(channel.Id),
                    cancellationToken));
            }
        }

        await Task.WhenAll(tasks);
    }

    public async Task AddUserToChannelGroupAsync(
        UserId userId,
        GuildChannelId channelId,
        CancellationToken cancellationToken = default)
    {
        var connectionIds = _connectionTracker.GetConnectionIds(userId);
        if (connectionIds.Count == 0)
            return;

        var tasks = new List<Task>(connectionIds.Count);

        foreach (var connectionId in connectionIds)
        {
            tasks.Add(_hubContext.Groups.AddToGroupAsync(
                connectionId,
                RealtimeHub.GetChannelGroupName(channelId),
                cancellationToken));
        }

        await Task.WhenAll(tasks);
    }

    public async Task AddAllGuildMembersToChannelGroupAsync(
        GuildId guildId,
        GuildChannelId channelId,
        CancellationToken cancellationToken = default)
    {
        var members = await _guildMemberRepository.GetGuildMembersAsync(guildId, cancellationToken);
        var tasks = new List<Task>();

        foreach (var member in members)
        {
            var connectionIds = _connectionTracker.GetConnectionIds(member.UserId);
            foreach (var connectionId in connectionIds)
            {
                tasks.Add(_hubContext.Groups.AddToGroupAsync(
                    connectionId,
                    RealtimeHub.GetChannelGroupName(channelId),
                    cancellationToken));
            }
        }

        if (tasks.Count > 0)
            await Task.WhenAll(tasks);
    }

    public async Task AddUserToConversationGroupAsync(
        UserId userId,
        ConversationId conversationId,
        CancellationToken cancellationToken = default)
    {
        var connectionIds = _connectionTracker.GetConnectionIds(userId);
        if (connectionIds.Count == 0)
            return;

        var tasks = new List<Task>(connectionIds.Count);

        foreach (var connectionId in connectionIds)
        {
            tasks.Add(_hubContext.Groups.AddToGroupAsync(
                connectionId,
                RealtimeHub.GetConversationGroupName(conversationId),
                cancellationToken));
        }

        await Task.WhenAll(tasks);
    }
}
