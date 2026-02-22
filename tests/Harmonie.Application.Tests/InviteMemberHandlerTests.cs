using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Guilds.InviteMember;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests;

public sealed class InviteMemberHandlerTests
{
    private readonly Mock<IGuildRepository> _guildRepositoryMock;
    private readonly Mock<IGuildMemberRepository> _guildMemberRepositoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly InviteMemberHandler _handler;

    public InviteMemberHandlerTests()
    {
        _guildRepositoryMock = new Mock<IGuildRepository>();
        _guildMemberRepositoryMock = new Mock<IGuildMemberRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();

        _handler = new InviteMemberHandler(
            _guildRepositoryMock.Object,
            _guildMemberRepositoryMock.Object,
            _userRepositoryMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WhenInviterIsNotAdmin_ShouldReturnForbidden()
    {
        var guild = CreateGuild();
        var inviterUserId = UserId.New();
        var request = new InviteMemberRequest(UserId.New().ToString());

        _guildRepositoryMock
            .Setup(x => x.GetByIdAsync(guild.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guild);

        _guildMemberRepositoryMock
            .Setup(x => x.GetRoleAsync(guild.Id, inviterUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildRole.Member);

        var response = await _handler.HandleAsync(guild.Id, request, inviterUserId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.InviteForbidden);
    }

    [Fact]
    public async Task HandleAsync_WhenTargetUserDoesNotExist_ShouldReturnNotFound()
    {
        var guild = CreateGuild();
        var inviterUserId = UserId.New();
        var targetUserId = UserId.New();
        var request = new InviteMemberRequest(targetUserId.ToString());

        _guildRepositoryMock
            .Setup(x => x.GetByIdAsync(guild.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guild);

        _guildMemberRepositoryMock
            .Setup(x => x.GetRoleAsync(guild.Id, inviterUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildRole.Admin);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var response = await _handler.HandleAsync(guild.Id, request, inviterUserId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.InviteTargetNotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenTargetAlreadyMember_ShouldReturnConflict()
    {
        var guild = CreateGuild();
        var inviterUserId = UserId.New();
        var targetUserId = UserId.New();
        var request = new InviteMemberRequest(targetUserId.ToString());

        _guildRepositoryMock
            .Setup(x => x.GetByIdAsync(guild.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guild);

        _guildMemberRepositoryMock
            .Setup(x => x.GetRoleAsync(guild.Id, inviterUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildRole.Admin);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateUser(targetUserId));

        _guildMemberRepositoryMock
            .Setup(x => x.IsMemberAsync(guild.Id, targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var response = await _handler.HandleAsync(guild.Id, request, inviterUserId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.MemberAlreadyExists);
    }

    [Fact]
    public async Task HandleAsync_WithValidRequest_ShouldSucceed()
    {
        var guild = CreateGuild();
        var inviterUserId = UserId.New();
        var targetUserId = UserId.New();
        var request = new InviteMemberRequest(targetUserId.ToString());

        _guildRepositoryMock
            .Setup(x => x.GetByIdAsync(guild.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guild);

        _guildMemberRepositoryMock
            .Setup(x => x.GetRoleAsync(guild.Id, inviterUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildRole.Admin);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateUser(targetUserId));

        _guildMemberRepositoryMock
            .Setup(x => x.IsMemberAsync(guild.Id, targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _guildMemberRepositoryMock
            .Setup(x => x.TryAddAsync(It.IsAny<GuildMember>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var response = await _handler.HandleAsync(guild.Id, request, inviterUserId);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();
        response.Data!.GuildId.Should().Be(guild.Id.ToString());
        response.Data.UserId.Should().Be(targetUserId.ToString());
        response.Data.Role.Should().Be(GuildRole.Member.ToString());
    }

    private static Guild CreateGuild()
    {
        var nameResult = GuildName.Create("Guild Alpha");
        if (nameResult.IsFailure)
            throw new InvalidOperationException("Failed to create guild name for tests.");

        var guildResult = Guild.Create(nameResult.Value!, UserId.New());
        if (guildResult.IsFailure)
            throw new InvalidOperationException("Failed to create guild for tests.");

        return guildResult.Value!;
    }

    private static User CreateUser(UserId userId)
    {
        var emailResult = Email.Create($"{Guid.NewGuid():N}@harmonie.chat");
        if (emailResult.IsFailure)
            throw new InvalidOperationException("Failed to create email for tests.");

        var usernameResult = Username.Create($"user{Guid.NewGuid():N}"[..20]);
        if (usernameResult.IsFailure)
            throw new InvalidOperationException("Failed to create username for tests.");

        return User.Rehydrate(
            userId,
            emailResult.Value!,
            usernameResult.Value!,
            "hash",
            avatarUrl: null,
            isEmailVerified: true,
            isActive: true,
            lastLoginAtUtc: null,
            displayName: null,
            bio: null,
            createdAtUtc: DateTime.UtcNow,
            updatedAtUtc: DateTime.UtcNow);
    }
}
