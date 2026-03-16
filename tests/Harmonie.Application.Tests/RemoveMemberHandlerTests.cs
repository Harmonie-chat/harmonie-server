using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Guilds.RemoveMember;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests;

public sealed class RemoveMemberHandlerTests
{
    private readonly Mock<IGuildRepository> _guildRepositoryMock;
    private readonly Mock<IGuildMemberRepository> _guildMemberRepositoryMock;
    private readonly RemoveMemberHandler _handler;

    public RemoveMemberHandlerTests()
    {
        _guildRepositoryMock = new Mock<IGuildRepository>();
        _guildMemberRepositoryMock = new Mock<IGuildMemberRepository>();

        _handler = new RemoveMemberHandler(
            _guildRepositoryMock.Object,
            _guildMemberRepositoryMock.Object,
            new Mock<IRealtimeGroupManager>().Object,
            NullLogger<RemoveMemberHandler>.Instance);
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
    public async Task HandleAsync_WhenCallerIsNotMember_ShouldReturnAccessDenied()
    {
        var guild = CreateGuild();
        var callerId = UserId.New();
        var targetId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, null));

        var response = await _handler.HandleAsync(guild.Id, callerId, targetId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsMemberNotAdmin_ShouldReturnAccessDenied()
    {
        var guild = CreateGuild();
        var callerId = UserId.New();
        var targetId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Member));

        var response = await _handler.HandleAsync(guild.Id, callerId, targetId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WhenTargetIsNotMember_ShouldReturnMemberNotFound()
    {
        var ownerId = UserId.New();
        var guild = CreateGuild(ownerId);
        var targetId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        _guildMemberRepositoryMock
            .Setup(x => x.GetRoleAsync(guild.Id, targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildRole?)null);

        var response = await _handler.HandleAsync(guild.Id, ownerId, targetId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.MemberNotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenTargetIsOwner_ShouldReturnOwnerCannotBeRemoved()
    {
        var ownerId = UserId.New();
        var callerId = UserId.New();
        var guild = CreateGuild(ownerId);

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        _guildMemberRepositoryMock
            .Setup(x => x.GetRoleAsync(guild.Id, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildRole.Admin);

        var response = await _handler.HandleAsync(guild.Id, callerId, ownerId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.OwnerCannotBeRemoved);
    }

    [Fact]
    public async Task HandleAsync_WhenAdminRemovesRegularMember_ShouldSucceedAndCallRemoveAsync()
    {
        var ownerId = UserId.New();
        var callerId = UserId.New();
        var targetId = UserId.New();
        var guild = CreateGuild(ownerId);

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        _guildMemberRepositoryMock
            .Setup(x => x.GetRoleAsync(guild.Id, targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildRole.Member);

        _guildMemberRepositoryMock
            .Setup(x => x.RemoveAsync(guild.Id, targetId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(guild.Id, callerId, targetId);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().BeTrue();

        _guildMemberRepositoryMock.Verify(
            x => x.RemoveAsync(guild.Id, targetId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static Guild CreateGuild(UserId? ownerId = null)
    {
        var nameResult = GuildName.Create("Remove Member Test Guild");
        if (nameResult.IsFailure)
            throw new InvalidOperationException("Failed to create guild name for tests.");

        var guildResult = Guild.Create(nameResult.Value!, ownerId ?? UserId.New());
        if (guildResult.IsFailure)
            throw new InvalidOperationException("Failed to create guild for tests.");

        return guildResult.Value!;
    }
}
