using System.Security.Claims;
using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Harmonie.API.RealTime;

[Authorize]
public sealed class TextChannelsHub : Hub
{
    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IGuildMemberRepository _guildMemberRepository;

    public TextChannelsHub(
        IGuildChannelRepository guildChannelRepository,
        IGuildMemberRepository guildMemberRepository)
    {
        _guildChannelRepository = guildChannelRepository;
        _guildMemberRepository = guildMemberRepository;
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

    internal static string GetChannelGroupName(GuildChannelId channelId)
        => $"channel:{channelId}";

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
