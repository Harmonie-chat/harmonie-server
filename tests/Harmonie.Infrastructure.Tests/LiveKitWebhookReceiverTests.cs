using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Harmonie.Infrastructure.Configuration;
using Harmonie.Infrastructure.LiveKit;
using Livekit.Server.Sdk.Dotnet;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Harmonie.Infrastructure.Tests;

public sealed class LiveKitWebhookReceiverTests
{
    private const string ApiKey = "testkey";
    private const string ApiSecret = "testsecret-that-is-long-enough-for-hmac";

    private readonly LiveKitWebhookReceiver _receiver;

    public LiveKitWebhookReceiverTests()
    {
        var settings = Options.Create(new LiveKitSettings
        {
            Url = "ws://localhost:7880",
            ApiKey = ApiKey,
            ApiSecret = ApiSecret
        });

        _receiver = new LiveKitWebhookReceiver(
            settings,
            NullLogger<LiveKitWebhookReceiver>.Instance);
    }

    [Fact]
    public void Receive_WhenSignatureIsInvalid_ShouldFail()
    {
        const string payload = """{"event":"participant_joined"}""";

        var result = _receiver.Receive(payload, "Bearer invalid-token");

        result.Success.Should().BeFalse();
        result.Event.Should().BeNull();
        result.ErrorDetail.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Receive_WhenSignatureIsValid_ShouldReturnParsedEvent()
    {
        var channelId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var payload = $$"""
            {
              "event": "participant_joined",
              "room": {
                "name": "channel:{{channelId}}"
              },
              "participant": {
                "identity": "{{userId}}",
                "name": "alice"
              },
              "createdAt": "1741388465"
            }
            """;

        var authorizationHeader = CreateAuthorizationHeader(payload);
        var result = _receiver.Receive(payload, authorizationHeader);

        result.Success.Should().BeTrue();
        result.Event.Should().NotBeNull();
        result.Event!.EventType.Should().Be("participant_joined");
        result.Event.RoomName.Should().Be($"channel:{channelId}");
        result.Event.ParticipantIdentity.Should().Be(userId.ToString());
        result.Event.ParticipantName.Should().Be("alice");
        result.Event.OccurredAtUtc.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1741388465).UtcDateTime);
    }

    private static string CreateAuthorizationHeader(string rawBody)
    {
        var checksum = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawBody)));
        var token = new AccessToken(ApiKey, ApiSecret)
            .WithSha256(checksum)
            .ToJwt();

        return $"Bearer {token}";
    }
}
