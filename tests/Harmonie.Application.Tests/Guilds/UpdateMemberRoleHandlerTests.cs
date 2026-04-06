using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Guilds.UpdateMemberRole;
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

public sealed class UpdateMemberRoleHandlerTests
{
    private readonly Mock<IGuildRepository> _guildRepositoryMock;
    private readonly Mock<IGuildMemberRepository> _guildMemberRepositoryMock;
    private readonly Mock<IGuildNotifier> _guildNotifierMock;
    private readonly UpdateMemberRoleHandler _handler;

    public UpdateMemberRoleHandlerTests()
    {
        _guildRepositoryMock = new Mock<IGuildRepository>();
        _guildMemberRepositoryMock = new Mock<IGuildMemberRepository>();
        _guildNotifierMock = new Mock<IGuildNotifier>();

        _handler = new UpdateMemberRoleHandler(
            _guildRepositoryMock.Object,
            _guildMemberRepositoryMock.Object,
            _guildNotifierMock.Object);
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

        var response = await _handler.HandleAsync(new UpdateMemberRoleInput(guildId, targetId, GuildRole.Admin), callerId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.NotFound);
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

        var response = await _handler.HandleAsync(new UpdateMemberRoleInput(guild.Id, targetId, GuildRole.Admin), callerId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
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

        var response = await _handler.HandleAsync(new UpdateMemberRoleInput(guild.Id, targetId, GuildRole.Admin), callerId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WhenTargetIsNotMember_ShouldReturnMemberNotFound()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
        var callerId = UserId.New();
        var targetId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        _guildMemberRepositoryMock
            .Setup(x => x.GetRoleAsync(guild.Id, targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildRole?)null);

        var response = await _handler.HandleAsync(new UpdateMemberRoleInput(guild.Id, targetId, GuildRole.Admin), callerId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.MemberNotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenTargetIsOwner_ShouldReturnOwnerRoleCannotBeChanged()
    {
        var ownerId = UserId.New();
        var guild = ApplicationTestBuilders.CreateGuild(ownerId);
        var callerId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        _guildMemberRepositoryMock
            .Setup(x => x.GetRoleAsync(guild.Id, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildRole.Admin);

        var response = await _handler.HandleAsync(new UpdateMemberRoleInput(guild.Id, ownerId, GuildRole.Member), callerId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.OwnerRoleCannotBeChanged);
    }

    [Fact]
    public async Task HandleAsync_WhenPromotingMemberToAdmin_ShouldSucceed()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
        var callerId = UserId.New();
        var targetId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        _guildMemberRepositoryMock
            .Setup(x => x.GetRoleAsync(guild.Id, targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildRole.Member);

        _guildMemberRepositoryMock
            .Setup(x => x.UpdateRoleAsync(guild.Id, targetId, GuildRole.Admin, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var response = await _handler.HandleAsync(new UpdateMemberRoleInput(guild.Id, targetId, GuildRole.Admin), callerId);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().BeTrue();

        _guildMemberRepositoryMock.Verify(
            x => x.UpdateRoleAsync(guild.Id, targetId, GuildRole.Admin, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenDemotingAdminToMember_ShouldSucceed()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
        var callerId = UserId.New();
        var targetId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        _guildMemberRepositoryMock
            .Setup(x => x.GetRoleAsync(guild.Id, targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildRole.Admin);

        _guildMemberRepositoryMock
            .Setup(x => x.UpdateRoleAsync(guild.Id, targetId, GuildRole.Member, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var response = await _handler.HandleAsync(new UpdateMemberRoleInput(guild.Id, targetId, GuildRole.Member), callerId);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().BeTrue();

        _guildMemberRepositoryMock.Verify(
            x => x.UpdateRoleAsync(guild.Id, targetId, GuildRole.Member, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenRoleUpdated_ShouldCallNotifyMemberRoleUpdatedAsync()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
        var callerId = UserId.New();
        var targetId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        _guildMemberRepositoryMock
            .Setup(x => x.GetRoleAsync(guild.Id, targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildRole.Member);

        _guildMemberRepositoryMock
            .Setup(x => x.UpdateRoleAsync(guild.Id, targetId, GuildRole.Admin, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _guildNotifierMock
            .Setup(x => x.NotifyMemberRoleUpdatedAsync(It.IsAny<MemberRoleUpdatedNotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _handler.HandleAsync(new UpdateMemberRoleInput(guild.Id, targetId, GuildRole.Admin), callerId);

        _guildNotifierMock.Verify(
            x => x.NotifyMemberRoleUpdatedAsync(
                It.Is<MemberRoleUpdatedNotification>(n =>
                    n.GuildId == guild.Id &&
                    n.UserId == targetId &&
                    n.NewRole == GuildRole.Admin),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

}
