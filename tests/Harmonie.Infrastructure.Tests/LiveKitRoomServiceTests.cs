using FluentAssertions;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Domain.Entities.Guilds;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Users;
using Harmonie.Infrastructure.LiveKit;
using Livekit.Server.Sdk.Dotnet;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Infrastructure.Tests;

public sealed class LiveKitRoomServiceTests
{
    private readonly Mock<IGuildChannelRepository> _guildChannelRepositoryMock;
    private readonly FakeLiveKitRoomApiClient _roomApiClient;
    private readonly LiveKitRoomService _service;

    public LiveKitRoomServiceTests()
    {
        _guildChannelRepositoryMock = new Mock<IGuildChannelRepository>();
        _roomApiClient = new FakeLiveKitRoomApiClient();

        _service = new LiveKitRoomService(
            _guildChannelRepositoryMock.Object,
            _roomApiClient,
            NullLogger<LiveKitRoomService>.Instance);
    }

    [Fact]
    public async Task GetGuildVoiceParticipantsAsync_ShouldReturnOnlyActiveVoiceChannelsWithValidParticipants()
    {
        var guildId = GuildId.New();
        var activeVoiceChannel = CreateChannel(guildId, "Voice 1", GuildChannelType.Voice, false, 1);
        var inactiveVoiceChannel = CreateChannel(guildId, "Voice 2", GuildChannelType.Voice, false, 2);
        var textChannel = CreateChannel(guildId, "general", GuildChannelType.Text, true, 0);
        var validUserId = UserId.New();

        _guildChannelRepositoryMock
            .Setup(x => x.GetByGuildIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([textChannel, activeVoiceChannel, inactiveVoiceChannel]);

        _roomApiClient.Rooms =
        [
            new Room { Name = $"channel:{activeVoiceChannel.Id}" }
        ];
        _roomApiClient.ParticipantsByRoomName[$"channel:{activeVoiceChannel.Id}"] =
        [
            new ParticipantInfo { Identity = validUserId.ToString(), Name = "alice" },
            new ParticipantInfo { Identity = "not-a-user-id", Name = "ignored" }
        ];

        var result = await _service.GetGuildVoiceParticipantsAsync(guildId, CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].ChannelId.Should().Be(activeVoiceChannel.Id);
        result[0].Participants.Should().HaveCount(1);
        result[0].Participants[0].UserId.Should().Be(validUserId);
        result[0].Participants[0].Username.Should().Be("alice");
        _roomApiClient.ListParticipantsCalls.Should().ContainSingle();
        _roomApiClient.ListParticipantsCalls[0].Should().Be($"channel:{activeVoiceChannel.Id}");
    }

    [Fact]
    public async Task GetGuildVoiceParticipantsAsync_WhenActiveRoomHasNoParticipants_ShouldOmitChannel()
    {
        var guildId = GuildId.New();
        var voiceChannel = CreateChannel(guildId, "Voice 1", GuildChannelType.Voice, false, 1);

        _guildChannelRepositoryMock
            .Setup(x => x.GetByGuildIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([voiceChannel]);

        _roomApiClient.Rooms =
        [
            new Room { Name = $"channel:{voiceChannel.Id}" }
        ];
        _roomApiClient.ParticipantsByRoomName[$"channel:{voiceChannel.Id}"] = [];

        var result = await _service.GetGuildVoiceParticipantsAsync(guildId, CancellationToken.None);

        result.Should().BeEmpty();
    }

    private static GuildChannel CreateChannel(
        GuildId guildId,
        string name,
        GuildChannelType type,
        bool isDefault,
        int position)
    {
        var channelResult = GuildChannel.Create(
            guildId,
            name,
            type,
            isDefault,
            position,
            new DateTime(2026, 7, 14, 12, 0, 0, DateTimeKind.Utc));
        if (channelResult.IsFailure || channelResult.Value is null)
            throw new InvalidOperationException("Failed to create channel for tests.");

        return channelResult.Value;
    }

    private sealed class FakeLiveKitRoomApiClient : ILiveKitRoomApiClient
    {
        public IReadOnlyList<Room> Rooms { get; set; } = [];

        public Dictionary<string, IReadOnlyList<ParticipantInfo>> ParticipantsByRoomName { get; } = new(StringComparer.Ordinal);

        public List<string> ListParticipantsCalls { get; } = [];

        public Task<IReadOnlyList<Room>> ListRoomsAsync(CancellationToken cancellationToken)
            => Task.FromResult(Rooms);

        public Task<IReadOnlyList<ParticipantInfo>> ListParticipantsAsync(
            string roomName,
            CancellationToken cancellationToken)
        {
            ListParticipantsCalls.Add(roomName);
            return Task.FromResult(
                ParticipantsByRoomName.TryGetValue(roomName, out var participants)
                    ? participants
                    : (IReadOnlyList<ParticipantInfo>)[]);
        }
    }
}
