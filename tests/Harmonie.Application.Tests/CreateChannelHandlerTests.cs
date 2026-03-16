using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Guilds.CreateChannel;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests;

public sealed class CreateChannelHandlerTests
{
    private readonly Mock<IGuildRepository> _guildRepositoryMock;
    private readonly Mock<IGuildChannelRepository> _guildChannelRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly CreateChannelHandler _handler;

    public CreateChannelHandlerTests()
    {
        _guildRepositoryMock = new Mock<IGuildRepository>();
        _guildChannelRepositoryMock = new Mock<IGuildChannelRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = new Mock<IUnitOfWorkTransaction>();

        _unitOfWorkMock
            .Setup(x => x.BeginAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_transactionMock.Object);

        _transactionMock
            .Setup(x => x.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _transactionMock
            .Setup(x => x.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        _handler = new CreateChannelHandler(
            _guildRepositoryMock.Object,
            _guildChannelRepositoryMock.Object,
            new Mock<IRealtimeGroupManager>().Object,
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

        var response = await _handler.HandleAsync(guildId, callerId, "general", GuildChannelType.Text, 0);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsNotMember_ShouldReturnAccessDenied()
    {
        var guild = CreateGuild();
        var callerId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, null));

        var response = await _handler.HandleAsync(guild.Id, callerId, "general", GuildChannelType.Text, 0);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsMemberNotAdmin_ShouldReturnAccessDenied()
    {
        var guild = CreateGuild();
        var callerId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Member));

        var response = await _handler.HandleAsync(guild.Id, callerId, "general", GuildChannelType.Text, 0);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WhenNameAlreadyExistsInGuild_ShouldReturnNameConflict()
    {
        var guild = CreateGuild();
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

        var response = await _handler.HandleAsync(guild.Id, adminId, "announcements", GuildChannelType.Text, 2);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Channel.NameConflict);
    }

    [Fact]
    public async Task HandleAsync_WhenAdminCreatesTextChannel_ShouldReturnCreatedChannel()
    {
        var guild = CreateGuild();
        var adminId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        var response = await _handler.HandleAsync(guild.Id, adminId, "announcements", GuildChannelType.Text, 2);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();
        response.Data!.GuildId.Should().Be(guild.Id.ToString());
        response.Data.Name.Should().Be("announcements");
        response.Data.Type.Should().Be("Text");
        response.Data.IsDefault.Should().BeFalse();
        response.Data.Position.Should().Be(2);
        response.Data.ChannelId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task HandleAsync_WhenAdminCreatesVoiceChannel_ShouldReturnCreatedChannel()
    {
        var guild = CreateGuild();
        var adminId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        var response = await _handler.HandleAsync(guild.Id, adminId, "Gaming", GuildChannelType.Voice, 5);

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
        var guild = CreateGuild();
        var adminId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        await _handler.HandleAsync(guild.Id, adminId, "lounge", GuildChannelType.Text, 3);

        _guildChannelRepositoryMock.Verify(
            x => x.AddAsync(It.IsAny<GuildChannel>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _transactionMock.Verify(
            x => x.CommitAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static Guild CreateGuild()
    {
        var nameResult = GuildName.Create("Create Channel Test Guild");
        if (nameResult.IsFailure)
            throw new InvalidOperationException("Failed to create guild name for tests.");

        var guildResult = Guild.Create(nameResult.Value!, UserId.New());
        if (guildResult.IsFailure)
            throw new InvalidOperationException("Failed to create guild for tests.");

        return guildResult.Value!;
    }
}
