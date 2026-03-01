using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Guilds.TransferOwnership;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests;

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

        _unitOfWorkMock
            .Setup(x => x.BeginAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_transactionMock.Object);

        _transactionMock
            .Setup(x => x.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _transactionMock
            .Setup(x => x.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        _handler = new TransferOwnershipHandler(
            _guildRepositoryMock.Object,
            _guildMemberRepositoryMock.Object,
            _unitOfWorkMock.Object,
            NullLogger<TransferOwnershipHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WhenGuildNotFound_ShouldReturnNotFound()
    {
        var guildId = GuildId.New();
        var callerId = UserId.New();
        var newOwnerId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetByIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guild?)null);

        var response = await _handler.HandleAsync(guildId, callerId, newOwnerId);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsNotOwner_ShouldReturnAccessDenied()
    {
        var ownerId = UserId.New();
        var guild = CreateGuild(ownerId);
        var callerId = UserId.New();
        var newOwnerId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetByIdAsync(guild.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guild);

        var response = await _handler.HandleAsync(guild.Id, callerId, newOwnerId);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerTransfersToSelf_ShouldReturnOwnerTransferToSelf()
    {
        var ownerId = UserId.New();
        var guild = CreateGuild(ownerId);

        _guildRepositoryMock
            .Setup(x => x.GetByIdAsync(guild.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guild);

        var response = await _handler.HandleAsync(guild.Id, ownerId, ownerId);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.OwnerTransferToSelf);
    }

    [Fact]
    public async Task HandleAsync_WhenNewOwnerIsNotMember_ShouldReturnMemberNotFound()
    {
        var ownerId = UserId.New();
        var guild = CreateGuild(ownerId);
        var newOwnerId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetByIdAsync(guild.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guild);

        _guildMemberRepositoryMock
            .Setup(x => x.GetRoleAsync(guild.Id, newOwnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildRole?)null);

        var response = await _handler.HandleAsync(guild.Id, ownerId, newOwnerId);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.MemberNotFound);
        _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenNewOwnerIsMember_ShouldSucceedAndCommitTransaction()
    {
        var ownerId = UserId.New();
        var guild = CreateGuild(ownerId);
        var newOwnerId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetByIdAsync(guild.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guild);

        _guildMemberRepositoryMock
            .Setup(x => x.GetRoleAsync(guild.Id, newOwnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildRole.Member);

        _guildRepositoryMock
            .Setup(x => x.UpdateOwnerAsync(guild.Id, newOwnerId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _guildMemberRepositoryMock
            .Setup(x => x.UpdateRoleAsync(guild.Id, newOwnerId, GuildRole.Admin, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var response = await _handler.HandleAsync(guild.Id, ownerId, newOwnerId);

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
        var guild = CreateGuild(ownerId);
        var newOwnerId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetByIdAsync(guild.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guild);

        _guildMemberRepositoryMock
            .Setup(x => x.GetRoleAsync(guild.Id, newOwnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildRole.Admin);

        _guildRepositoryMock
            .Setup(x => x.UpdateOwnerAsync(guild.Id, newOwnerId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _guildMemberRepositoryMock
            .Setup(x => x.UpdateRoleAsync(guild.Id, newOwnerId, GuildRole.Admin, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var response = await _handler.HandleAsync(guild.Id, ownerId, newOwnerId);

        response.Success.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_WhenMemberDeletedConcurrently_ShouldReturnMemberNotFoundAndNotCommit()
    {
        var ownerId = UserId.New();
        var guild = CreateGuild(ownerId);
        var newOwnerId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetByIdAsync(guild.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guild);

        // Membership check passes (member exists at this point)
        _guildMemberRepositoryMock
            .Setup(x => x.GetRoleAsync(guild.Id, newOwnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildRole.Member);

        _guildRepositoryMock
            .Setup(x => x.UpdateOwnerAsync(guild.Id, newOwnerId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // UpdateRoleAsync returns 0: member was deleted concurrently between GetRoleAsync and UpdateRoleAsync
        _guildMemberRepositoryMock
            .Setup(x => x.UpdateRoleAsync(guild.Id, newOwnerId, GuildRole.Admin, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var response = await _handler.HandleAsync(guild.Id, ownerId, newOwnerId);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.MemberNotFound);

        _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Guild CreateGuild(UserId? ownerId = null)
    {
        var nameResult = GuildName.Create("Transfer Ownership Test Guild");
        if (nameResult.IsFailure)
            throw new InvalidOperationException("Failed to create guild name for tests.");

        var guildResult = Guild.Create(nameResult.Value!, ownerId ?? UserId.New());
        if (guildResult.IsFailure)
            throw new InvalidOperationException("Failed to create guild for tests.");

        return guildResult.Value!;
    }
}
