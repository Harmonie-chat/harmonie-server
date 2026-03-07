using Harmonie.Domain.ValueObjects;

namespace Harmonie.Application.Interfaces;

public interface ILiveKitTokenService
{
    Task<string> GenerateRoomTokenAsync(GuildChannelId channelId, UserId userId, string username, CancellationToken ct);
}
