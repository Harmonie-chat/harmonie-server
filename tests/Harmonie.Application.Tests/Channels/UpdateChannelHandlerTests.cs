using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Channels.UpdateChannel;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Tests.Common;
using Harmonie.Domain.Entities.Guilds;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Users;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Channels;

public sealed class UpdateChannelHandlerTests
{
    private readonly Mock<IGuildChannelRepository> _guildChannelRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly UpdateChannelHandler _handler;

    public UpdateChannelHandlerTests()
    {
        _guildChannelRepositoryMock = new Mock<IGuildChannelRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = new Mock<IUnitOfWorkTransaction>();

        _transactionMock = _unitOfWorkMock.SetupTransactionMock();

        _handler = new UpdateChannelHandler(
            _guildChannelRepositoryMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WhenChannelDoesNotExist_ShouldReturnNotFound()
    {
        var channelId = GuildChannelId.New();
        var callerId = UserId.New();

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channelId, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChannelAccessContext?)null);

        var request = new UpdateChannelRequest(Name: "new-name");
        var response = await _handler.HandleAsync(new UpdateChannelInput(channelId, request), callerId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Channel.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsNotMember_ShouldReturnChannelAccessDenied()
    {
        var channel = CreateChannel();
        var callerId = UserId.New();

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, CallerRole: null));

        var request = new UpdateChannelRequest(Name: "new-name");
        var response = await _handler.HandleAsync(new UpdateChannelInput(channel.Id, request), callerId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Channel.AccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsMemberNotAdmin_ShouldReturnGuildAccessDenied()
    {
        var channel = CreateChannel();
        var callerId = UserId.New();

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Member));

        var request = new UpdateChannelRequest(Name: "new-name");
        var response = await _handler.HandleAsync(new UpdateChannelInput(channel.Id, request), callerId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WhenNameAlreadyExistsInGuild_ShouldReturnNameConflict()
    {
        var channel = CreateChannel();
        var adminId = UserId.New();

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Admin));

        _guildChannelRepositoryMock
            .Setup(x => x.ExistsByNameInGuildAsync(
                channel.GuildId,
                "existing-channel",
                channel.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new UpdateChannelRequest(Name: "existing-channel");
        var response = await _handler.HandleAsync(new UpdateChannelInput(channel.Id, request), adminId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Channel.NameConflict);
    }

    [Fact]
    public async Task HandleAsync_WhenAdminUpdatesName_ShouldReturnUpdatedChannel()
    {
        var channel = CreateChannel("old-name");
        var adminId = UserId.New();

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Admin));

        _guildChannelRepositoryMock
            .Setup(x => x.ExistsByNameInGuildAsync(
                channel.GuildId,
                "new-name",
                channel.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var request = new UpdateChannelRequest(Name: "new-name");
        var response = await _handler.HandleAsync(new UpdateChannelInput(channel.Id, request), adminId);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();
        response.Data!.ChannelId.Should().Be(channel.Id.ToString());
        response.Data.Name.Should().Be("new-name");
    }

    [Fact]
    public async Task HandleAsync_WhenAdminUpdatesPosition_ShouldReturnUpdatedChannel()
    {
        var channel = CreateChannel(position: 0);
        var adminId = UserId.New();

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Admin));

        var request = new UpdateChannelRequest(Position: 5);
        var response = await _handler.HandleAsync(new UpdateChannelInput(channel.Id, request), adminId);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();
        response.Data!.Position.Should().Be(5);
    }

    [Fact]
    public async Task HandleAsync_WhenAdminUpdatesChannel_ShouldPersistAndCommit()
    {
        var channel = CreateChannel("old-name");
        var adminId = UserId.New();

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Admin));

        _guildChannelRepositoryMock
            .Setup(x => x.ExistsByNameInGuildAsync(
                channel.GuildId,
                "new-name",
                channel.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var request = new UpdateChannelRequest(Name: "new-name");
        await _handler.HandleAsync(new UpdateChannelInput(channel.Id, request), adminId);

        _guildChannelRepositoryMock.Verify(
            x => x.UpdateAsync(It.IsAny<GuildChannel>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _transactionMock.Verify(
            x => x.CommitAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenNoFieldsSet_ShouldSucceedWithoutUpdating()
    {
        var channel = CreateChannel("unchanged");
        var adminId = UserId.New();

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Admin));

        var request = new UpdateChannelRequest();
        var response = await _handler.HandleAsync(new UpdateChannelInput(channel.Id, request), adminId);

        response.Success.Should().BeTrue();
        response.Data!.Name.Should().Be("unchanged");

        _guildChannelRepositoryMock.Verify(
            x => x.ExistsByNameInGuildAsync(
                It.IsAny<GuildId>(),
                It.IsAny<string>(),
                It.IsAny<GuildChannelId>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _guildChannelRepositoryMock.Verify(
            x => x.UpdateAsync(It.IsAny<GuildChannel>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _unitOfWorkMock.Verify(
            x => x.BeginAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenFieldsAreExplicitlyNull_ShouldTreatThemAsNotProvided()
    {
        var channel = CreateChannel("unchanged", position: 2);
        var adminId = UserId.New();

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Admin));

        var request = new UpdateChannelRequest(Name: null, Position: null);
        var response = await _handler.HandleAsync(new UpdateChannelInput(channel.Id, request), adminId);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Name.Should().Be("unchanged");
        response.Data.Position.Should().Be(2);

        _guildChannelRepositoryMock.Verify(
            x => x.UpdateAsync(It.IsAny<GuildChannel>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static GuildChannel CreateChannel(string name = "general", int position = 0)
    {
        var result = GuildChannel.Create(
            GuildId.New(),
            name,
            GuildChannelType.Text,
            isDefault: false,
            position);

        if (result.IsFailure)
            throw new InvalidOperationException("Failed to create channel for tests.");

        return result.Value!;
    }
}
