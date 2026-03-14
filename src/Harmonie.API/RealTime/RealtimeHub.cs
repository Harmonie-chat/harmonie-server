using System.Collections.Concurrent;
using System.Security.Claims;
using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Harmonie.API.RealTime;

[Authorize]
public sealed class RealtimeHub : Hub
{
    private static readonly ConcurrentDictionary<string, DateTime> _typingThrottles = new();
    private static readonly TimeSpan TypingThrottleInterval = TimeSpan.FromSeconds(5);

    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IGuildMemberRepository _guildMemberRepository;
    private readonly IGuildRepository _guildRepository;
    private readonly IConversationRepository _conversationRepository;

    public RealtimeHub(
        IGuildChannelRepository guildChannelRepository,
        IGuildMemberRepository guildMemberRepository,
        IGuildRepository guildRepository,
        IConversationRepository conversationRepository)
    {
        _guildChannelRepository = guildChannelRepository;
        _guildMemberRepository = guildMemberRepository;
        _guildRepository = guildRepository;
        _conversationRepository = conversationRepository;
    }

    public async Task JoinChannel(Guid channelId)
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

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            GetChannelGroupName(parsedChannelId),
            Context.ConnectionAborted);
    }

    public async Task LeaveChannel(Guid channelId)
    {
        if (channelId == Guid.Empty)
            throw new HubException(ApplicationErrorCodes.Common.ValidationFailed);

        var parsedChannelId = GuildChannelId.From(channelId);
        await Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            GetChannelGroupName(parsedChannelId),
            Context.ConnectionAborted);
    }

    public async Task JoinGuild(Guid guildId)
    {
        if (guildId == Guid.Empty)
            throw new HubException(ApplicationErrorCodes.Common.ValidationFailed);

        if (!TryGetAuthenticatedUserId(out var currentUserId) || currentUserId is null)
            throw new HubException(ApplicationErrorCodes.Auth.InvalidCredentials);

        var parsedGuildId = GuildId.From(guildId);
        var guildAccess = await _guildRepository.GetWithCallerRoleAsync(
            parsedGuildId,
            currentUserId,
            Context.ConnectionAborted);

        if (guildAccess is null)
            throw new HubException(ApplicationErrorCodes.Guild.NotFound);

        if (guildAccess.CallerRole is null)
            throw new HubException(ApplicationErrorCodes.Guild.AccessDenied);

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            GetGuildGroupName(parsedGuildId),
            Context.ConnectionAborted);
    }

    public async Task LeaveGuild(Guid guildId)
    {
        if (guildId == Guid.Empty)
            throw new HubException(ApplicationErrorCodes.Common.ValidationFailed);

        var parsedGuildId = GuildId.From(guildId);
        await Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            GetGuildGroupName(parsedGuildId),
            Context.ConnectionAborted);
    }

    public async Task JoinConversation(Guid conversationId)
    {
        if (conversationId == Guid.Empty)
            throw new HubException(ApplicationErrorCodes.Common.ValidationFailed);

        if (!TryGetAuthenticatedUserId(out var currentUserId) || currentUserId is null)
            throw new HubException(ApplicationErrorCodes.Auth.InvalidCredentials);

        var parsedConversationId = ConversationId.From(conversationId);
        var conversation = await _conversationRepository.GetByIdAsync(
            parsedConversationId,
            Context.ConnectionAborted);

        if (conversation is null)
            throw new HubException(ApplicationErrorCodes.Conversation.NotFound);

        if (conversation.User1Id != currentUserId && conversation.User2Id != currentUserId)
            throw new HubException(ApplicationErrorCodes.Conversation.AccessDenied);

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            GetConversationGroupName(parsedConversationId),
            Context.ConnectionAborted);
    }

    public async Task LeaveConversation(Guid conversationId)
    {
        if (conversationId == Guid.Empty)
            throw new HubException(ApplicationErrorCodes.Common.ValidationFailed);

        var parsedConversationId = ConversationId.From(conversationId);
        await Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            GetConversationGroupName(parsedConversationId),
            Context.ConnectionAborted);
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
            UserId: currentUserId.ToString(),
            ChannelId: channelId.ToString(),
            Timestamp: DateTime.UtcNow);

        await Clients.GroupExcept(
            GetChannelGroupName(parsedChannelId),
            Context.ConnectionId)
            .SendAsync("UserTyping", payload, Context.ConnectionAborted);
    }

    public async Task StartTypingConversation(Guid conversationId)
    {
        if (conversationId == Guid.Empty)
            throw new HubException(ApplicationErrorCodes.Common.ValidationFailed);

        if (!TryGetAuthenticatedUserId(out var currentUserId) || currentUserId is null)
            throw new HubException(ApplicationErrorCodes.Auth.InvalidCredentials);

        var parsedConversationId = ConversationId.From(conversationId);
        var conversation = await _conversationRepository.GetByIdAsync(
            parsedConversationId,
            Context.ConnectionAborted);

        if (conversation is null)
            throw new HubException(ApplicationErrorCodes.Conversation.NotFound);

        if (conversation.User1Id != currentUserId && conversation.User2Id != currentUserId)
            throw new HubException(ApplicationErrorCodes.Conversation.AccessDenied);

        var throttleKey = $"conversation:{currentUserId}:{conversationId}";
        if (!TryPassThrottle(throttleKey))
            return;

        var payload = new ConversationUserTypingEvent(
            UserId: currentUserId.ToString(),
            ConversationId: conversationId.ToString(),
            Timestamp: DateTime.UtcNow);

        await Clients.GroupExcept(
            GetConversationGroupName(parsedConversationId),
            Context.ConnectionId)
            .SendAsync("ConversationUserTyping", payload, Context.ConnectionAborted);
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
    string UserId,
    string ChannelId,
    DateTime Timestamp);

public sealed record ConversationUserTypingEvent(
    string UserId,
    string ConversationId,
    DateTime Timestamp);
