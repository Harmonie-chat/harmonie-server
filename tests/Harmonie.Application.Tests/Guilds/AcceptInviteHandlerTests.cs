using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Guilds.AcceptInvite;
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

public sealed class AcceptInviteHandlerTests
{
    private readonly Mock<IGuildInviteRepository> _guildInviteRepositoryMock;
    private readonly Mock<IGuildMemberRepository> _guildMemberRepositoryMock;
    private readonly Mock<IGuildBanRepository> _guildBanRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly AcceptInviteHandler _handler;

    private readonly GuildId _guildId = GuildId.New();
    private readonly UserId _creatorId = UserId.New();
    private readonly UserId _callerId = UserId.New();
    private const string InviteCode = "ABCD1234";

    public AcceptInviteHandlerTests()
    {
        _guildInviteRepositoryMock = new Mock<IGuildInviteRepository>();
        _guildMemberRepositoryMock = new Mock<IGuildMemberRepository>();
        _guildBanRepositoryMock = new Mock<IGuildBanRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = new Mock<IUnitOfWorkTransaction>();

        _transactionMock = _unitOfWorkMock.SetupTransactionMock();

        _handler = new AcceptInviteHandler(
            _guildInviteRepositoryMock.Object,
            _guildMemberRepositoryMock.Object,
            _guildBanRepositoryMock.Object,
            new Mock<IRealtimeGroupManager>().Object,
            _unitOfWorkMock.Object,
            NullLogger<AcceptInviteHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WhenInviteNotFound_ShouldReturnNotFound()
    {
        _guildInviteRepositoryMock
            .Setup(x => x.GetAcceptDetailsByCodeAsync(InviteCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InviteAcceptDetails?)null);

        var response = await _handler.HandleAsync(InviteCode, _callerId);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Invite.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenInviteExpired_ShouldReturnExpired()
    {
        var details = new InviteAcceptDetails(
            _guildId, _creatorId, UsesCount: 0, MaxUses: null,
            ExpiresAtUtc: DateTime.UtcNow.AddHours(-1));

        _guildInviteRepositoryMock
            .Setup(x => x.GetAcceptDetailsByCodeAsync(InviteCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        var response = await _handler.HandleAsync(InviteCode, _callerId);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Invite.Expired);
    }

    [Fact]
    public async Task HandleAsync_WhenInviteExhausted_ShouldReturnExhausted()
    {
        var details = new InviteAcceptDetails(
            _guildId, _creatorId, UsesCount: 10, MaxUses: 10,
            ExpiresAtUtc: null);

        _guildInviteRepositoryMock
            .Setup(x => x.GetAcceptDetailsByCodeAsync(InviteCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        var response = await _handler.HandleAsync(InviteCode, _callerId);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Invite.Exhausted);
    }

    [Fact]
    public async Task HandleAsync_WhenAlreadyMember_ShouldReturnConflict()
    {
        var details = new InviteAcceptDetails(
            _guildId, _creatorId, UsesCount: 0, MaxUses: null,
            ExpiresAtUtc: null);

        _guildInviteRepositoryMock
            .Setup(x => x.GetAcceptDetailsByCodeAsync(InviteCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        _guildMemberRepositoryMock
            .Setup(x => x.IsMemberAsync(_guildId, _callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var response = await _handler.HandleAsync(InviteCode, _callerId);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.MemberAlreadyExists);
    }

    [Fact]
    public async Task HandleAsync_WithValidInvite_ShouldReturnSuccess()
    {
        var details = new InviteAcceptDetails(
            _guildId, _creatorId, UsesCount: 2, MaxUses: 10,
            ExpiresAtUtc: DateTime.UtcNow.AddHours(24));

        _guildInviteRepositoryMock
            .Setup(x => x.GetAcceptDetailsByCodeAsync(InviteCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        _guildMemberRepositoryMock
            .Setup(x => x.IsMemberAsync(_guildId, _callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _guildMemberRepositoryMock
            .Setup(x => x.TryAddAsync(It.IsAny<GuildMember>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var response = await _handler.HandleAsync(InviteCode, _callerId);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.GuildId.Should().Be(_guildId.Value);
        response.Data.UserId.Should().Be(_callerId.Value);
        response.Data.Role.Should().Be(GuildRole.Member.ToString());

        _guildInviteRepositoryMock.Verify(
            x => x.IncrementUsesCountAsync(InviteCode, It.IsAny<CancellationToken>()),
            Times.Once);
        _transactionMock.Verify(
            x => x.CommitAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenTryAddFails_ShouldReturnConflict()
    {
        var details = new InviteAcceptDetails(
            _guildId, _creatorId, UsesCount: 0, MaxUses: null,
            ExpiresAtUtc: null);

        _guildInviteRepositoryMock
            .Setup(x => x.GetAcceptDetailsByCodeAsync(InviteCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        _guildMemberRepositoryMock
            .Setup(x => x.IsMemberAsync(_guildId, _callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _guildMemberRepositoryMock
            .Setup(x => x.TryAddAsync(It.IsAny<GuildMember>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var response = await _handler.HandleAsync(InviteCode, _callerId);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.MemberAlreadyExists);
    }
}
