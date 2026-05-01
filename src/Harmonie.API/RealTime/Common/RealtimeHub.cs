using System.Collections.Concurrent;
using System.Security.Claims;
using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Harmonie.API.RealTime.Common;

[Authorize]
public sealed class RealtimeHub : Hub<IRealtimeClient>
{
    private static readonly ConcurrentDictionary<string, DateTime> _typingThrottles = new();
    private static readonly TimeSpan TypingThrottleInterval = TimeSpan.FromSeconds(5);

    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IGuildMemberRepository _guildMemberRepository;
    private readonly IConversationRepository _conversationRepository;
    private readonly IConnectionTracker _connectionTracker;
    private readonly IRealtimeGroupManager _realtimeGroupManager;

    public RealtimeHub(
        IGuildChannelRepository guildChannelRepository,
        IGuildMemberRepository guildMemberRepository,
        IConversationRepository conversationRepository,
        IConnectionTracker connectionTracker,
        IRealtimeGroupManager realtimeGroupManager)
    {
        _guildChannelRepository = guildChannelRepository;
        _guildMemberRepository = guildMemberRepository;
        _conversationRepository = conversationRepository;
        _connectionTracker = connectionTracker;
        _realtimeGroupManager = realtimeGroupManager;
    }

    public override async Task OnConnectedAsync()
    {
        if (TryGetAuthenticatedUserId(out var userId) && userId is not null)
        {
            await _connectionTracker.HandleConnectedAsync(
                userId, Context.ConnectionId, Context.ConnectionAborted);

            await _realtimeGroupManager.SubscribeConnectionAsync(
                userId, Context.ConnectionId, Context.ConnectionAborted);
        }

        await Clients.Caller.Ready(Context.ConnectionAborted);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (TryGetAuthenticatedUserId(out var userId) && userId is not null)
        {
            await _connectionTracker.HandleDisconnectedAsync(
                userId, Context.ConnectionId, Context.ConnectionAborted);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task StartTypingChannel(Guid channelId)
    {
        if (channelId == Guid.Empty)
            throw new HubException(ApplicationErrorCodes.Common.ValidationFailed);

        if (!TryGetAuthenticatedUserId(out var currentUserId) || currentUserId is null)
            throw new HubException(ApplicationErrorCodes.Auth.InvalidCredentials);

        var parsedChannelId = GuildChannelId.From(channelId);
        var channel = await _guildChannelRepository.GetByIdAsync(parsedChannelId, Context.ConnectionAborted);
        if (channel is null)
            throw new HubException(ApplicationErrorCodes.Channel.NotFound);

        if (channel.Type != GuildChannelType.Text)
            throw new HubException(ApplicationErrorCodes.Channel.NotText);

        var isMember = await _guildMemberRepository.IsMemberAsync(
            channel.GuildId,
            currentUserId,
            Context.ConnectionAborted);
        if (!isMember)
            throw new HubException(ApplicationErrorCodes.Channel.AccessDenied);

        var throttleKey = $"channel:{currentUserId}:{channelId}";
        if (!TryPassThrottle(throttleKey))
            return;

        var payload = new UserTypingEvent(
            UserId: currentUserId.Value,
            ChannelId: channelId,
            Timestamp: DateTime.UtcNow);

        await Clients.GroupExcept(
            GetChannelGroupName(parsedChannelId),
            Context.ConnectionId)
            .UserTyping(payload, Context.ConnectionAborted);
    }

    public async Task StartTypingConversation(Guid conversationId)
    {
        if (conversationId == Guid.Empty)
            throw new HubException(ApplicationErrorCodes.Common.ValidationFailed);

        if (!TryGetAuthenticatedUserId(out var currentUserId) || currentUserId is null)
            throw new HubException(ApplicationErrorCodes.Auth.InvalidCredentials);

        var parsedConversationId = ConversationId.From(conversationId);
        var access = await _conversationRepository.GetByIdWithParticipantCheckAsync(
            parsedConversationId,
            currentUserId,
            Context.ConnectionAborted);

        if (access is null)
            throw new HubException(ApplicationErrorCodes.Conversation.NotFound);

        if (access.Participant is null)
            throw new HubException(ApplicationErrorCodes.Conversation.AccessDenied);

        var throttleKey = $"conversation:{currentUserId}:{conversationId}";
        if (!TryPassThrottle(throttleKey))
            return;

        var payload = new ConversationUserTypingEvent(
            UserId: currentUserId.Value,
            ConversationId: conversationId,
            Timestamp: DateTime.UtcNow);

        await Clients.GroupExcept(
            GetConversationGroupName(parsedConversationId),
            Context.ConnectionId)
            .ConversationUserTyping(payload, Context.ConnectionAborted);
    }

    internal static string GetChannelGroupName(GuildChannelId channelId)
        => $"channel:{channelId}";

    internal static string GetGuildGroupName(GuildId guildId)
        => $"guild-voice:{guildId}";

    internal static string GetConversationGroupName(ConversationId conversationId)
        => $"conversation:{conversationId}";

    private static bool TryPassThrottle(string throttleKey)
    {
        var now = DateTime.UtcNow;

        if (_typingThrottles.TryGetValue(throttleKey, out var lastSent)
            && now - lastSent < TypingThrottleInterval)
            return false;

        _typingThrottles[throttleKey] = now;
        return true;
    }

    private bool TryGetAuthenticatedUserId(out UserId? userId)
    {
        userId = null;

        var principal = Context.User;
        if (principal?.Identity?.IsAuthenticated != true)
            return false;

        var claimValue = principal.FindFirst("sub")?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(claimValue))
            return false;

        if (!UserId.TryParse(claimValue, out var parsedUserId) || parsedUserId is null)
            return false;

        userId = parsedUserId;
        return true;
    }
}

public sealed record UserTypingEvent(
    Guid UserId,
    Guid ChannelId,
    DateTime Timestamp);

public sealed record ConversationUserTypingEvent(
    Guid UserId,
    Guid ConversationId,
    DateTime Timestamp);
