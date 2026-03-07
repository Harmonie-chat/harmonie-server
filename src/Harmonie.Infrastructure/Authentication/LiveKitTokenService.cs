using Harmonie.Application.Interfaces;
using Harmonie.Domain.ValueObjects;
using Harmonie.Infrastructure.Configuration;
using Livekit.Server.Sdk.Dotnet;
using Microsoft.Extensions.Options;

namespace Harmonie.Infrastructure.Authentication;

public sealed class LiveKitTokenService : ILiveKitTokenService
{
    private readonly LiveKitSettings _settings;

    public LiveKitTokenService(IOptions<LiveKitSettings> settings) => _settings = settings.Value;

    public Task<string> GenerateRoomTokenAsync(GuildChannelId channelId, UserId userId, string username, CancellationToken ct)
    {
        var jwt = new AccessToken(_settings.ApiKey, _settings.ApiSecret)
            .WithIdentity(userId.ToString())
            .WithName(username)
            .WithGrants(new VideoGrants { RoomJoin = true, Room = $"channel:{channelId}" })
            .ToJwt();

        return Task.FromResult(jwt);
    }
}
