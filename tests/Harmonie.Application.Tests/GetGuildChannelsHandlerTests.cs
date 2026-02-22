using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Guilds.GetGuildChannels;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests;

public sealed class GetGuildChannelsHandlerTests
{
    private readonly Mock<IGuildRepository> _guildRepositoryMock;
    private readonly Mock<IGuildMemberRepository> _guildMemberRepositoryMock;
    private readonly Mock<IGuildChannelRepository> _guildChannelRepositoryMock;
    private readonly GetGuildChannelsHandler _handler;

    public GetGuildChannelsHandlerTests()
    {
        _guildRepositoryMock = new Mock<IGuildRepository>();
        _guildMemberRepositoryMock = new Mock<IGuildMemberRepository>();
        _guildChannelRepositoryMock = new Mock<IGuildChannelRepository>();

        _handler = new GetGuildChannelsHandler(
            _guildRepositoryMock.Object,
            _guildMemberRepositoryMock.Object,
            _guildChannelRepositoryMock.Object,
            NullLogger<GetGuildChannelsHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WhenGuildDoesNotExist_ShouldReturnNotFound()
    {
        var guildId = GuildId.New();
        var userId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetByIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guild?)null);

        var response = await _handler.HandleAsync(guildId, userId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenUserIsNotMember_ShouldReturnAccessDenied()
    {
        var guild = CreateGuild();
        var userId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetByIdAsync(guild.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guild);

        _guildMemberRepositoryMock
            .Setup(x => x.IsMemberAsync(guild.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var response = await _handler.HandleAsync(guild.Id, userId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WithValidMember_ShouldReturnChannels()
    {
        var guild = CreateGuild();
        var userId = UserId.New();
        var textChannel = CreateChannel(guild.Id, "general", GuildChannelType.Text, true, 0);
        var voiceChannel = CreateChannel(guild.Id, "General Voice", GuildChannelType.Voice, true, 1);

        _guildRepositoryMock
            .Setup(x => x.GetByIdAsync(guild.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guild);

        _guildMemberRepositoryMock
            .Setup(x => x.IsMemberAsync(guild.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _guildChannelRepositoryMock
            .Setup(x => x.GetByGuildIdAsync(guild.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([textChannel, voiceChannel]);

        var response = await _handler.HandleAsync(guild.Id, userId);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();
        response.Data!.GuildId.Should().Be(guild.Id.ToString());
        response.Data.Channels.Should().HaveCount(2);
        response.Data.Channels[0].Type.Should().Be("Text");
        response.Data.Channels[1].Type.Should().Be("Voice");
    }

    private static Guild CreateGuild()
    {
        var nameResult = GuildName.Create("Guild Alpha");
        if (nameResult.IsFailure)
            throw new InvalidOperationException("Failed to create guild name for tests.");

        var guildResult = Guild.Create(nameResult.Value!, UserId.New());
        if (guildResult.IsFailure)
            throw new InvalidOperationException("Failed to create guild for tests.");

        return guildResult.Value!;
    }

    private static GuildChannel CreateChannel(
        GuildId guildId,
        string name,
        GuildChannelType type,
        bool isDefault,
        int position)
    {
        var channelResult = GuildChannel.Create(guildId, name, type, isDefault, position);
        if (channelResult.IsFailure)
            throw new InvalidOperationException("Failed to create channel for tests.");

        return channelResult.Value!;
    }
}
