using System.Security.Claims;
using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Harmonie.API.RealTime;

[Authorize]
public sealed class VoicePresenceHub : Hub
{
    private readonly IGuildRepository _guildRepository;

    public VoicePresenceHub(IGuildRepository guildRepository)
    {
        _guildRepository = guildRepository;
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

    internal static string GetGuildGroupName(GuildId guildId)
        => $"guild-voice:{guildId}";

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
