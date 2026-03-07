using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Harmonie.Domain.ValueObjects;
using Harmonie.Infrastructure.Authentication;
using Harmonie.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace Harmonie.Infrastructure.Tests;

public sealed class LiveKitTokenServiceTests
{
    private readonly LiveKitTokenService _service;
    private readonly string _apiKey = "testkey";
    private readonly string _apiSecret = "testsecret-that-is-long-enough-for-hmac";

    public LiveKitTokenServiceTests()
    {
        var settings = Options.Create(new LiveKitSettings
        {
            PublicUrl = "ws://localhost:7880",
            ApiKey = _apiKey,
            ApiSecret = _apiSecret,
        });
        _service = new LiveKitTokenService(settings);
    }

    [Fact]
    public async Task GenerateRoomTokenAsync_ShouldReturnValidJwt()
    {
        var channelId = GuildChannelId.New();
        var userId = UserId.New();
        const string username = "testuser";

        var token = await _service.GenerateRoomTokenAsync(channelId, userId, username, CancellationToken.None);

        token.Token.Should().NotBeNullOrWhiteSpace();
        token.Token.Split('.').Should().HaveCount(3, "a JWT must have three segments");
    }

    [Fact]
    public async Task GenerateRoomTokenAsync_ShouldEmbedUserIdAsIdentity()
    {
        var channelId = GuildChannelId.New();
        var userId = UserId.New();
        const string username = "testuser";

        var token = await _service.GenerateRoomTokenAsync(channelId, userId, username, CancellationToken.None);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token.Token);
        var sub = jwt.Subject;

        sub.Should().Be(userId.ToString());
    }

    [Fact]
    public async Task GenerateRoomTokenAsync_ShouldEmbedRoomNameWithChannelConvention()
    {
        var channelId = GuildChannelId.New();
        var userId = UserId.New();
        const string username = "testuser";

        var token = await _service.GenerateRoomTokenAsync(channelId, userId, username, CancellationToken.None);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token.Token);

        // LiveKit embeds VideoGrants in the "video" claim as a JSON object
        var videoClaim = jwt.Claims.FirstOrDefault(c => c.Type == "video");
        videoClaim.Should().NotBeNull();
        videoClaim!.Value.Should().Contain($"channel:{channelId}");
        token.Url.Should().Be("ws://localhost:7880");
        token.RoomName.Should().Be($"channel:{channelId}");
    }

    [Fact]
    public async Task GenerateRoomTokenAsync_DifferentChannels_ShouldProduceDifferentTokens()
    {
        var userId = UserId.New();
        var channelId1 = GuildChannelId.New();
        var channelId2 = GuildChannelId.New();

        var token1 = await _service.GenerateRoomTokenAsync(channelId1, userId, "user", CancellationToken.None);
        var token2 = await _service.GenerateRoomTokenAsync(channelId2, userId, "user", CancellationToken.None);

        token1.Token.Should().NotBe(token2.Token);
    }
}
