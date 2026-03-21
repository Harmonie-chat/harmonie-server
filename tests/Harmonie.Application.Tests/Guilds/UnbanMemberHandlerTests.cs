using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Guilds.UnbanMember;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Application.Tests.Common;
using Harmonie.Domain.Entities.Guilds;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;


namespace Harmonie.Application.Tests.Guilds;

public sealed class UnbanMemberHandlerTests
{
    private readonly Mock<IGuildRepository> _guildRepositoryMock;
    private readonly Mock<IGuildBanRepository> _guildBanRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly UnbanMemberHandler _handler;

    public UnbanMemberHandlerTests()
    {
        _guildRepositoryMock = new Mock<IGuildRepository>();
        _guildBanRepositoryMock = new Mock<IGuildBanRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = new Mock<IUnitOfWorkTransaction>();

        _transactionMock = _unitOfWorkMock.SetupTransactionMock();

        _handler = new UnbanMemberHandler(
            _guildRepositoryMock.Object,
            _guildBanRepositoryMock.Object,
            _unitOfWorkMock.Object,
            NullLogger<UnbanMemberHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WhenGuildNotFound_ShouldReturnNotFound()
    {
        var guildId = GuildId.New();
        var callerId = UserId.New();
        var targetId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guildId, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildAccessContext?)null);

        var response = await _handler.HandleAsync(guildId, callerId, targetId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsNotAdmin_ShouldReturnAccessDenied()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
        var callerId = UserId.New();
        var targetId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Member));

        var response = await _handler.HandleAsync(guild.Id, callerId, targetId);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsNotMember_ShouldReturnAccessDenied()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
        var callerId = UserId.New();
        var targetId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, null));

        var response = await _handler.HandleAsync(guild.Id, callerId, targetId);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WhenUserIsNotBanned_ShouldReturnNotBanned()
    {
        var ownerId = UserId.New();
        var targetId = UserId.New();
        var guild = ApplicationTestBuilders.CreateGuild(ownerId);

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        _guildBanRepositoryMock
            .Setup(x => x.DeleteAsync(guild.Id, targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var response = await _handler.HandleAsync(guild.Id, ownerId, targetId);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.NotBanned);
    }

    [Fact]
    public async Task HandleAsync_WhenAdminUnbansBannedUser_ShouldSucceed()
    {
        var ownerId = UserId.New();
        var targetId = UserId.New();
        var guild = ApplicationTestBuilders.CreateGuild(ownerId);

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        _guildBanRepositoryMock
            .Setup(x => x.DeleteAsync(guild.Id, targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var response = await _handler.HandleAsync(guild.Id, ownerId, targetId);

        response.Success.Should().BeTrue();

        _guildBanRepositoryMock.Verify(
            x => x.DeleteAsync(guild.Id, targetId, It.IsAny<CancellationToken>()),
            Times.Once);

        _transactionMock.Verify(
            x => x.CommitAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

}
