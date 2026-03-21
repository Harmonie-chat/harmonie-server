using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Guilds.CreateGuildInvite;
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

public sealed class CreateGuildInviteHandlerTests
{
    private readonly Mock<IGuildRepository> _guildRepositoryMock;
    private readonly Mock<IGuildInviteRepository> _guildInviteRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly CreateGuildInviteHandler _handler;

    public CreateGuildInviteHandlerTests()
    {
        _guildRepositoryMock = new Mock<IGuildRepository>();
        _guildInviteRepositoryMock = new Mock<IGuildInviteRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = new Mock<IUnitOfWorkTransaction>();

        _transactionMock = _unitOfWorkMock.SetupTransactionMock();

        _handler = new CreateGuildInviteHandler(
            _guildRepositoryMock.Object,
            _guildInviteRepositoryMock.Object,
            _unitOfWorkMock.Object,
            NullLogger<CreateGuildInviteHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WhenGuildNotFound_ShouldReturnNotFound()
    {
        var guildId = GuildId.New();
        var callerId = UserId.New();
        var request = new CreateGuildInviteRequest();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guildId, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildAccessContext?)null);

        var response = await _handler.HandleAsync(guildId, request, callerId);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.NotFound);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsNotAdmin_ShouldReturnForbidden()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
        var callerId = UserId.New();
        var request = new CreateGuildInviteRequest();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Member));

        var response = await _handler.HandleAsync(guild.Id, request, callerId);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.InviteForbidden);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsNotMember_ShouldReturnForbidden()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
        var callerId = UserId.New();
        var request = new CreateGuildInviteRequest();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, null));

        var response = await _handler.HandleAsync(guild.Id, request, callerId);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.InviteForbidden);
    }

    [Fact]
    public async Task HandleAsync_WithValidRequest_ShouldCreateInvite()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
        var callerId = UserId.New();
        var request = new CreateGuildInviteRequest(MaxUses: 10, ExpiresInHours: 24);

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        var response = await _handler.HandleAsync(guild.Id, request, callerId);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.GuildId.Should().Be(guild.Id.ToString());
        response.Data.CreatorId.Should().Be(callerId.ToString());
        response.Data.MaxUses.Should().Be(10);
        response.Data.UsesCount.Should().Be(0);
        response.Data.ExpiresAtUtc.Should().NotBeNull();
        response.Data.Code.Should().HaveLength(8);

        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Once);
        _guildInviteRepositoryMock.Verify(x => x.AddAsync(It.IsAny<GuildInvite>(), It.IsAny<CancellationToken>()), Times.Once);
        _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithNoLimits_ShouldCreateUnlimitedInvite()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
        var callerId = UserId.New();
        var request = new CreateGuildInviteRequest();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        var response = await _handler.HandleAsync(guild.Id, request, callerId);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.MaxUses.Should().BeNull();
        response.Data.ExpiresAtUtc.Should().BeNull();
    }

}
