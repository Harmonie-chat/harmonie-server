using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Guilds.TransferOwnership;
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

public sealed class TransferOwnershipHandlerTests
{
    private readonly Mock<IGuildRepository> _guildRepositoryMock;
    private readonly Mock<IGuildMemberRepository> _guildMemberRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly TransferOwnershipHandler _handler;

    public TransferOwnershipHandlerTests()
    {
        _guildRepositoryMock = new Mock<IGuildRepository>();
        _guildMemberRepositoryMock = new Mock<IGuildMemberRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = new Mock<IUnitOfWorkTransaction>();

        _transactionMock = _unitOfWorkMock.SetupTransactionMock();

        _handler = new TransferOwnershipHandler(
            _guildRepositoryMock.Object,
            _guildMemberRepositoryMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerTransfersToSelf_ShouldReturnOwnerTransferToSelf()
    {
        var ownerId = UserId.New();
        var guild = ApplicationTestBuilders.CreateGuild(ownerId);

        var response = await _handler.HandleAsync(new TransferOwnershipInput(guild.Id, ownerId), ownerId);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.OwnerTransferToSelf);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenGuildNotFound_ShouldReturnNotFound()
    {
        var callerId = UserId.New();
        var newOwnerId = UserId.New();
        var guildId = GuildId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guildId, newOwnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildAccessContext?)null);

        var response = await _handler.HandleAsync(new TransferOwnershipInput(guildId, newOwnerId), callerId);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsNotOwner_ShouldReturnAccessDenied()
    {
        var ownerId = UserId.New();
        var guild = ApplicationTestBuilders.CreateGuild(ownerId);
        var callerId = UserId.New();
        var newOwnerId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, newOwnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Member));

        var response = await _handler.HandleAsync(new TransferOwnershipInput(guild.Id, newOwnerId), callerId);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WhenNewOwnerIsNotMember_ShouldReturnMemberNotFound()
    {
        var ownerId = UserId.New();
        var guild = ApplicationTestBuilders.CreateGuild(ownerId);
        var newOwnerId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, newOwnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, null));

        var response = await _handler.HandleAsync(new TransferOwnershipInput(guild.Id, newOwnerId), ownerId);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.MemberNotFound);
        _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenNewOwnerIsMember_ShouldSucceedAndCommitTransaction()
    {
        var ownerId = UserId.New();
        var guild = ApplicationTestBuilders.CreateGuild(ownerId);
        var newOwnerId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, newOwnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Member));

        _guildRepositoryMock
            .Setup(x => x.UpdateOwnerAsync(guild.Id, newOwnerId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _guildMemberRepositoryMock
            .Setup(x => x.UpdateRoleAsync(guild.Id, newOwnerId, GuildRole.Admin, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var response = await _handler.HandleAsync(new TransferOwnershipInput(guild.Id, newOwnerId), ownerId);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().BeTrue();

        _guildRepositoryMock.Verify(
            x => x.UpdateOwnerAsync(guild.Id, newOwnerId, It.IsAny<CancellationToken>()),
            Times.Once);

        _guildMemberRepositoryMock.Verify(
            x => x.UpdateRoleAsync(guild.Id, newOwnerId, GuildRole.Admin, It.IsAny<CancellationToken>()),
            Times.Once);

        _transactionMock.Verify(
            x => x.CommitAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenNewOwnerIsAlreadyAdmin_ShouldSucceed()
    {
        var ownerId = UserId.New();
        var guild = ApplicationTestBuilders.CreateGuild(ownerId);
        var newOwnerId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, newOwnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        _guildRepositoryMock
            .Setup(x => x.UpdateOwnerAsync(guild.Id, newOwnerId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _guildMemberRepositoryMock
            .Setup(x => x.UpdateRoleAsync(guild.Id, newOwnerId, GuildRole.Admin, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var response = await _handler.HandleAsync(new TransferOwnershipInput(guild.Id, newOwnerId), ownerId);

        response.Success.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_WhenMemberDeletedConcurrently_ShouldReturnMemberNotFoundAndNotCommit()
    {
        var ownerId = UserId.New();
        var guild = ApplicationTestBuilders.CreateGuild(ownerId);
        var newOwnerId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, newOwnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Member));

        _guildRepositoryMock
            .Setup(x => x.UpdateOwnerAsync(guild.Id, newOwnerId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // UpdateRoleAsync returns 0: member was deleted concurrently
        _guildMemberRepositoryMock
            .Setup(x => x.UpdateRoleAsync(guild.Id, newOwnerId, GuildRole.Admin, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var response = await _handler.HandleAsync(new TransferOwnershipInput(guild.Id, newOwnerId), ownerId);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.MemberNotFound);

        _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

}
