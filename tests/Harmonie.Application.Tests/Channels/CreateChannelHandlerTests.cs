using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Guilds.CreateChannel;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Application.Tests.Common;
using Harmonie.Domain.Entities.Guilds;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Channels;

public sealed class CreateChannelHandlerTests
{
    private readonly Mock<IGuildRepository> _guildRepositoryMock;
    private readonly Mock<IGuildChannelRepository> _guildChannelRepositoryMock;
    private readonly Mock<IGuildNotifier> _guildNotifierMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly CreateChannelHandler _handler;

    public CreateChannelHandlerTests()
    {
        _guildRepositoryMock = new Mock<IGuildRepository>();
        _guildChannelRepositoryMock = new Mock<IGuildChannelRepository>();
        _guildNotifierMock = new Mock<IGuildNotifier>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = new Mock<IUnitOfWorkTransaction>();

        _transactionMock = _unitOfWorkMock.SetupTransactionMock();

        _guildNotifierMock
            .Setup(x => x.NotifyChannelCreatedAsync(
                It.IsAny<ChannelCreatedNotification>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _handler = new CreateChannelHandler(
            _guildRepositoryMock.Object,
            _guildChannelRepositoryMock.Object,
            new Mock<IRealtimeGroupManager>().Object,
            _guildNotifierMock.Object,
            _unitOfWorkMock.Object,
            NullLogger<CreateChannelHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WhenGuildDoesNotExist_ShouldReturnNotFound()
    {
        var guildId = GuildId.New();
        var callerId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guildId, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildAccessContext?)null);

        var response = await _handler.HandleAsync(new CreateChannelInput(guildId, "general", GuildChannelType.Text, 0), callerId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsNotMember_ShouldReturnAccessDenied()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
        var callerId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, null));

        var response = await _handler.HandleAsync(new CreateChannelInput(guild.Id, "general", GuildChannelType.Text, 0), callerId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsMemberNotAdmin_ShouldReturnAccessDenied()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
        var callerId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Member));

        var response = await _handler.HandleAsync(new CreateChannelInput(guild.Id, "general", GuildChannelType.Text, 0), callerId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WhenNameAlreadyExistsInGuild_ShouldReturnNameConflict()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
        var adminId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        _guildChannelRepositoryMock
            .Setup(x => x.ExistsByNameInGuildAsync(
                guild.Id,
                "announcements",
                It.IsAny<GuildChannelId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var response = await _handler.HandleAsync(new CreateChannelInput(guild.Id, "announcements", GuildChannelType.Text, 2), adminId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Channel.NameConflict);
    }

    [Fact]
    public async Task HandleAsync_WhenAdminCreatesTextChannel_ShouldReturnCreatedChannel()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
        var adminId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        var response = await _handler.HandleAsync(new CreateChannelInput(guild.Id, "announcements", GuildChannelType.Text, 2), adminId);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();
        response.Data!.GuildId.Should().Be(guild.Id.Value);
        response.Data.Name.Should().Be("announcements");
        response.Data.Type.Should().Be("Text");
        response.Data.IsDefault.Should().BeFalse();
        response.Data.Position.Should().Be(2);
        response.Data.ChannelId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_WhenAdminCreatesVoiceChannel_ShouldReturnCreatedChannel()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
        var adminId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        var response = await _handler.HandleAsync(new CreateChannelInput(guild.Id, "Gaming", GuildChannelType.Voice, 5), adminId);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();
        response.Data!.Type.Should().Be("Voice");
        response.Data.Name.Should().Be("Gaming");
        response.Data.Position.Should().Be(5);
    }

    [Fact]
    public async Task HandleAsync_WhenAdminCreatesChannel_ShouldPersistAndCommit()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
        var adminId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        await _handler.HandleAsync(new CreateChannelInput(guild.Id, "lounge", GuildChannelType.Text, 3), adminId);

        _guildChannelRepositoryMock.Verify(
            x => x.AddAsync(It.IsAny<GuildChannel>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _transactionMock.Verify(
            x => x.CommitAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenAdminCreatesChannel_ShouldSendChannelCreatedNotification()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
        var adminId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        var response = await _handler.HandleAsync(new CreateChannelInput(guild.Id, "lounge", GuildChannelType.Text, 3), adminId);

        response.Success.Should().BeTrue();

        _guildNotifierMock.Verify(
            x => x.NotifyChannelCreatedAsync(
                It.Is<ChannelCreatedNotification>(n =>
                    n.GuildId == guild.Id &&
                    n.Name == "lounge" &&
                    n.Type == GuildChannelType.Text &&
                    n.Position == 3),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenChannelCreationFails_ShouldNotSendNotification()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
        var adminId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        _guildChannelRepositoryMock
            .Setup(x => x.ExistsByNameInGuildAsync(
                guild.Id,
                "lounge",
                It.IsAny<GuildChannelId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var response = await _handler.HandleAsync(new CreateChannelInput(guild.Id, "lounge", GuildChannelType.Text, 3), adminId);

        response.Success.Should().BeFalse();

        _guildNotifierMock.Verify(
            x => x.NotifyChannelCreatedAsync(
                It.IsAny<ChannelCreatedNotification>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

}
