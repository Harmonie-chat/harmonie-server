using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Guilds.BanMember;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Application.Tests.Common;
using Harmonie.Domain.Entities.Guilds;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Guilds;

public sealed class BanMemberHandlerTests
{
    private readonly Mock<IGuildRepository> _guildRepositoryMock;
    private readonly Mock<IGuildMemberRepository> _guildMemberRepositoryMock;
    private readonly Mock<IGuildBanRepository> _guildBanRepositoryMock;
    private readonly Mock<IMessageRepository> _messageRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly BanMemberHandler _handler;

    public BanMemberHandlerTests()
    {
        _guildRepositoryMock = new Mock<IGuildRepository>();
        _guildMemberRepositoryMock = new Mock<IGuildMemberRepository>();
        _guildBanRepositoryMock = new Mock<IGuildBanRepository>();
        _messageRepositoryMock = new Mock<IMessageRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = new Mock<IUnitOfWorkTransaction>();

        _transactionMock = _unitOfWorkMock.SetupTransactionMock();

        _handler = new BanMemberHandler(
            _guildRepositoryMock.Object,
            _guildMemberRepositoryMock.Object,
            _guildBanRepositoryMock.Object,
            _messageRepositoryMock.Object,
            new Mock<IRealtimeGroupManager>().Object,
            _unitOfWorkMock.Object,
            NullLogger<BanMemberHandler>.Instance);
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

        var response = await _handler.HandleAsync(new BanMemberInput(guildId, targetId, null, 0), callerId);

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

        var response = await _handler.HandleAsync(new BanMemberInput(guild.Id, targetId, null, 0), callerId);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WhenBanSelf_ShouldReturnCannotBanSelf()
    {
        var ownerId = UserId.New();
        var guild = ApplicationTestBuilders.CreateGuild(ownerId);

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        var response = await _handler.HandleAsync(new BanMemberInput(guild.Id, ownerId, null, 0), ownerId);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.CannotBanSelf);
    }

    [Fact]
    public async Task HandleAsync_WhenBanOwner_ShouldReturnOwnerCannotBeBanned()
    {
        var ownerId = UserId.New();
        var callerId = UserId.New();
        var guild = ApplicationTestBuilders.CreateGuild(ownerId);

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        var response = await _handler.HandleAsync(new BanMemberInput(guild.Id, ownerId, null, 0), callerId);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.OwnerCannotBeBanned);
    }

    [Fact]
    public async Task HandleAsync_WhenNonOwnerAdminBansAdmin_ShouldReturnAccessDenied()
    {
        var ownerId = UserId.New();
        var callerId = UserId.New();
        var targetId = UserId.New();
        var guild = ApplicationTestBuilders.CreateGuild(ownerId);

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        _guildMemberRepositoryMock
            .Setup(x => x.GetRoleAsync(guild.Id, targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildRole.Admin);

        var response = await _handler.HandleAsync(new BanMemberInput(guild.Id, targetId, null, 0), callerId);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WhenOwnerBansAdmin_ShouldSucceed()
    {
        var ownerId = UserId.New();
        var targetId = UserId.New();
        var guild = ApplicationTestBuilders.CreateGuild(ownerId);

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        _guildMemberRepositoryMock
            .Setup(x => x.GetRoleAsync(guild.Id, targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildRole.Admin);

        _guildBanRepositoryMock
            .Setup(x => x.TryAddAsync(It.IsAny<GuildBan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var response = await _handler.HandleAsync(new BanMemberInput(guild.Id, targetId, "Abuse", 0), ownerId);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Reason.Should().Be("Abuse");

        _guildMemberRepositoryMock.Verify(
            x => x.RemoveAsync(guild.Id, targetId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenAlreadyBanned_ShouldReturnAlreadyBanned()
    {
        var ownerId = UserId.New();
        var targetId = UserId.New();
        var guild = ApplicationTestBuilders.CreateGuild(ownerId);

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        _guildMemberRepositoryMock
            .Setup(x => x.GetRoleAsync(guild.Id, targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildRole.Member);

        _guildBanRepositoryMock
            .Setup(x => x.TryAddAsync(It.IsAny<GuildBan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var response = await _handler.HandleAsync(new BanMemberInput(guild.Id, targetId, null, 0), ownerId);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.AlreadyBanned);
    }

    [Fact]
    public async Task HandleAsync_WhenBanMember_ShouldSucceedAndRemoveMember()
    {
        var ownerId = UserId.New();
        var targetId = UserId.New();
        var guild = ApplicationTestBuilders.CreateGuild(ownerId);

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        _guildMemberRepositoryMock
            .Setup(x => x.GetRoleAsync(guild.Id, targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildRole.Member);

        _guildBanRepositoryMock
            .Setup(x => x.TryAddAsync(It.IsAny<GuildBan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var response = await _handler.HandleAsync(new BanMemberInput(guild.Id, targetId, null, 0), ownerId);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.GuildId.Should().Be(guild.Id.Value);
        response.Data.UserId.Should().Be(targetId.Value);
        response.Data.BannedBy.Should().Be(ownerId.Value);

        _guildMemberRepositoryMock.Verify(
            x => x.RemoveAsync(guild.Id, targetId, It.IsAny<CancellationToken>()),
            Times.Once);

        _transactionMock.Verify(
            x => x.CommitAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenBanNonMember_ShouldSucceedWithoutRemove()
    {
        var ownerId = UserId.New();
        var targetId = UserId.New();
        var guild = ApplicationTestBuilders.CreateGuild(ownerId);

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        _guildMemberRepositoryMock
            .Setup(x => x.GetRoleAsync(guild.Id, targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildRole?)null);

        _guildBanRepositoryMock
            .Setup(x => x.TryAddAsync(It.IsAny<GuildBan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var response = await _handler.HandleAsync(new BanMemberInput(guild.Id, targetId, null, 0), ownerId);

        response.Success.Should().BeTrue();

        _guildMemberRepositoryMock.Verify(
            x => x.RemoveAsync(It.IsAny<GuildId>(), It.IsAny<UserId>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenPurgeMessagesDaysGreaterThanZero_ShouldCallSoftDelete()
    {
        var ownerId = UserId.New();
        var targetId = UserId.New();
        var guild = ApplicationTestBuilders.CreateGuild(ownerId);

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        _guildMemberRepositoryMock
            .Setup(x => x.GetRoleAsync(guild.Id, targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildRole.Member);

        _guildBanRepositoryMock
            .Setup(x => x.TryAddAsync(It.IsAny<GuildBan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _messageRepositoryMock
            .Setup(x => x.SoftDeleteByAuthorInGuildAsync(guild.Id, targetId, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var response = await _handler.HandleAsync(new BanMemberInput(guild.Id, targetId, null, 3), ownerId);

        response.Success.Should().BeTrue();

        _messageRepositoryMock.Verify(
            x => x.SoftDeleteByAuthorInGuildAsync(guild.Id, targetId, 3, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenPurgeMessagesDaysIsZero_ShouldNotCallSoftDelete()
    {
        var ownerId = UserId.New();
        var targetId = UserId.New();
        var guild = ApplicationTestBuilders.CreateGuild(ownerId);

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        _guildMemberRepositoryMock
            .Setup(x => x.GetRoleAsync(guild.Id, targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildRole.Member);

        _guildBanRepositoryMock
            .Setup(x => x.TryAddAsync(It.IsAny<GuildBan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var response = await _handler.HandleAsync(new BanMemberInput(guild.Id, targetId, null, 0), ownerId);

        response.Success.Should().BeTrue();

        _messageRepositoryMock.Verify(
            x => x.SoftDeleteByAuthorInGuildAsync(
                It.IsAny<GuildId>(), It.IsAny<UserId>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

}
