using Harmonie.Domain.ValueObjects;

namespace Harmonie.Application.Interfaces;

public sealed record LiveKitRoomToken(
    string Token,
    string Url,
    string RoomName);

public interface ILiveKitTokenService
{
    Task<LiveKitRoomToken> GenerateRoomTokenAsync(
        GuildChannelId channelId,
        UserId userId,
        string username,
        CancellationToken ct);
}
