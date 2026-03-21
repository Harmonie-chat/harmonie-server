using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Guilds.GetGuildVoiceParticipants;
using Harmonie.Application.Tests.Common;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Application.Interfaces.Voice;
using Harmonie.Domain.Entities.Guilds;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Voice;

public sealed class GetGuildVoiceParticipantsHandlerTests
{
    private readonly Mock<IGuildRepository> _guildRepositoryMock;
    private readonly Mock<IGuildMemberRepository> _guildMemberRepositoryMock;
    private readonly Mock<ILiveKitRoomService> _liveKitRoomServiceMock;
    private readonly GetGuildVoiceParticipantsHandler _handler;

    public GetGuildVoiceParticipantsHandlerTests()
    {
        _guildRepositoryMock = new Mock<IGuildRepository>();
        _guildMemberRepositoryMock = new Mock<IGuildMemberRepository>();
        _liveKitRoomServiceMock = new Mock<ILiveKitRoomService>();

        _handler = new GetGuildVoiceParticipantsHandler(
            _guildRepositoryMock.Object,
            _guildMemberRepositoryMock.Object,
            _liveKitRoomServiceMock.Object,
            NullLogger<GetGuildVoiceParticipantsHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WhenGuildDoesNotExist_ShouldReturnNotFound()
    {
        var guildId = GuildId.New();
        var userId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guildId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildAccessContext?)null);

        var response = await _handler.HandleAsync(guildId, userId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenUserIsNotMember_ShouldReturnAccessDenied()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
        var userId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, null));

        var response = await _handler.HandleAsync(guild.Id, userId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WithValidMember_ShouldReturnParticipantsGroupedByChannel()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
        var requesterUserId = UserId.New();
        var channelId = GuildChannelId.New();
        var participantUserId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, requesterUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Member));

        _liveKitRoomServiceMock
            .Setup(x => x.GetGuildVoiceParticipantsAsync(guild.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new GuildVoiceChannelParticipants(
                    channelId,
                    [new VoiceChannelParticipant(participantUserId, "alice")])
            ]);

        _guildMemberRepositoryMock
            .Setup(x => x.GetGuildMembersAsync(guild.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GuildMemberUser>());

        var response = await _handler.HandleAsync(guild.Id, requesterUserId);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();
        response.Data!.Channels.Should().HaveCount(1);
        response.Data.Channels[0].ChannelId.Should().Be(channelId.ToString());
        response.Data.Channels[0].Participants.Should().HaveCount(1);
        response.Data.Channels[0].Participants[0].UserId.Should().Be(participantUserId.ToString());
        response.Data.Channels[0].Participants[0].Username.Should().Be("alice");
    }

}
