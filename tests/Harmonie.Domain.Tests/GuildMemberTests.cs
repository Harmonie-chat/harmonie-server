using FluentAssertions;
using Harmonie.Domain.Entities;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Xunit;

namespace Harmonie.Domain.Tests;

public sealed class GuildMemberTests
{
    [Fact]
    public void Create_WithAdminRoleAndNoInviter_ShouldSucceed()
    {
        var guildId = GuildId.New();
        var userId = UserId.New();

        var result = GuildMember.Create(guildId, userId, GuildRole.Admin, invitedByUserId: null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.GuildId.Should().Be(guildId);
        result.Value.UserId.Should().Be(userId);
        result.Value.Role.Should().Be(GuildRole.Admin);
        result.Value.InvitedByUserId.Should().BeNull();
    }

    [Fact]
    public void Create_WithAdminRoleAndInviter_ShouldFail()
    {
        var result = GuildMember.Create(
            GuildId.New(),
            UserId.New(),
            GuildRole.Admin,
            invitedByUserId: UserId.New());

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Admin membership cannot have an inviter");
    }

    [Fact]
    public void Create_WithInvalidRole_ShouldFail()
    {
        var invalidRole = (GuildRole)999;

        var result = GuildMember.Create(
            GuildId.New(),
            UserId.New(),
            invalidRole,
            invitedByUserId: null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Guild role is invalid");
    }

    [Fact]
    public void Create_WithMemberRoleAndInviter_ShouldSucceed()
    {
        var inviterUserId = UserId.New();

        var result = GuildMember.Create(
            GuildId.New(),
            UserId.New(),
            GuildRole.Member,
            invitedByUserId: inviterUserId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Role.Should().Be(GuildRole.Member);
        result.Value.InvitedByUserId.Should().Be(inviterUserId);
    }

    [Fact]
    public void Rehydrate_WithInvalidRole_ShouldThrow()
    {
        var act = () => GuildMember.Rehydrate(
            GuildId.New(),
            UserId.New(),
            (GuildRole)999,
            DateTime.UtcNow,
            invitedByUserId: null);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
