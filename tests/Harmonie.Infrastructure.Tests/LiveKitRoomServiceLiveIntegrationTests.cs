using FluentAssertions;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Harmonie.Infrastructure.Configuration;
using Harmonie.Infrastructure.LiveKit;
using Livekit.Server.Sdk.Dotnet;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Harmonie.Infrastructure.Tests;

public sealed class LiveKitRoomServiceLiveIntegrationTests
{
    private const string DefaultPublicLiveKitUrl = "ws://localhost:7880";
    private const string DefaultInternalLiveKitUrl = "http://localhost:7880";
    private const string DefaultApiKey = "devkey";
    private const string DefaultApiSecret = "devsecret-that-is-long-enough-for-hmac-signing";

    [Fact]
    public async Task GetGuildVoiceParticipantsAsync_WhenRealLiveKitRoomExists_ShouldQueryLiveServerAndReturnNoParticipants()
    {
        var settings = CreateSettings();
        var guildId = GuildId.New();
        var voiceChannel = CreateChannel(guildId, "general-voice", GuildChannelType.Voice, true, 1);
        var roomName = $"channel:{voiceChannel.Id}";

        var guildChannelRepositoryMock = new Mock<IGuildChannelRepository>();
        guildChannelRepositoryMock
            .Setup(x => x.GetByGuildIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([voiceChannel]);

        var roomApiClient = new LiveKitSdkRoomApiClient(Options.Create(settings));
        var service = new LiveKitRoomService(
            guildChannelRepositoryMock.Object,
            roomApiClient,
            NullLogger<LiveKitRoomService>.Instance);

        using var httpClient = new HttpClient();
        var roomServiceClient = new RoomServiceClient(
            settings.GetInternalUrl(),
            settings.ApiKey,
            settings.ApiSecret,
            httpClient);

        await roomServiceClient.CreateRoom(new CreateRoomRequest
        {
            Name = roomName,
            EmptyTimeout = 300
        });

        try
        {
            var rooms = await roomApiClient.ListRoomsAsync(CancellationToken.None);
            rooms.Should().Contain(room => room.Name == roomName);

            var result = await service.GetGuildVoiceParticipantsAsync(guildId, CancellationToken.None);

            result.Should().BeEmpty("the room is active but has no connected participants");
        }
        finally
        {
            await roomServiceClient.DeleteRoom(new DeleteRoomRequest
            {
                Room = roomName
            });
        }
    }

    private static LiveKitSettings CreateSettings()
        => new()
        {
            PublicUrl = Environment.GetEnvironmentVariable("LIVEKIT_TEST_URL") ?? DefaultPublicLiveKitUrl,
            InternalUrl = Environment.GetEnvironmentVariable("LIVEKIT_TEST_INTERNAL_URL") ?? DefaultInternalLiveKitUrl,
            ApiKey = Environment.GetEnvironmentVariable("LIVEKIT_TEST_API_KEY") ?? DefaultApiKey,
            ApiSecret = Environment.GetEnvironmentVariable("LIVEKIT_TEST_API_SECRET") ?? DefaultApiSecret
        };

    private static GuildChannel CreateChannel(
        GuildId guildId,
        string name,
        GuildChannelType type,
        bool isDefault,
        int position)
    {
        var channelResult = GuildChannel.Create(guildId, name, type, isDefault, position);
        if (channelResult.IsFailure || channelResult.Value is null)
            throw new InvalidOperationException("Failed to create channel for tests.");

        return channelResult.Value;
    }
}
