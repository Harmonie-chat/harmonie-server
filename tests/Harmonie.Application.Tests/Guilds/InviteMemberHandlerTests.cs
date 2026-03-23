using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Guilds.InviteMember;
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

public sealed class InviteMemberHandlerTests
{
    private readonly Mock<IGuildRepository> _guildRepositoryMock;
    private readonly Mock<IGuildMemberRepository> _guildMemberRepositoryMock;
    private readonly InviteMemberHandler _handler;

    public InviteMemberHandlerTests()
    {
        _guildRepositoryMock = new Mock<IGuildRepository>();
        _guildMemberRepositoryMock = new Mock<IGuildMemberRepository>();

        _handler = new InviteMemberHandler(
            _guildRepositoryMock.Object,
            _guildMemberRepositoryMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WhenInviterIsNotAdmin_ShouldReturnForbidden()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
        var inviterUserId = UserId.New();
        var request = new InviteMemberRequest(UserId.New().ToString());

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, inviterUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Member));

        var response = await _handler.HandleAsync(new InviteMemberInput(guild.Id, request.UserId), inviterUserId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.InviteForbidden);
    }

    [Fact]
    public async Task HandleAsync_WhenTargetUserDoesNotExist_ShouldReturnNotFound()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
        var inviterUserId = UserId.New();
        var targetUserId = UserId.New();
        var request = new InviteMemberRequest(targetUserId.ToString());

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, inviterUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        _guildMemberRepositoryMock
            .Setup(x => x.GetInviteMemberTargetLookupAsync(guild.Id, targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InviteMemberTargetLookup(UserExists: false, IsMember: false));

        var response = await _handler.HandleAsync(new InviteMemberInput(guild.Id, request.UserId), inviterUserId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.InviteTargetNotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenTargetAlreadyMember_ShouldReturnConflict()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
        var inviterUserId = UserId.New();
        var targetUserId = UserId.New();
        var request = new InviteMemberRequest(targetUserId.ToString());

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, inviterUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        _guildMemberRepositoryMock
            .Setup(x => x.GetInviteMemberTargetLookupAsync(guild.Id, targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InviteMemberTargetLookup(UserExists: true, IsMember: true));

        var response = await _handler.HandleAsync(new InviteMemberInput(guild.Id, request.UserId), inviterUserId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.MemberAlreadyExists);
    }

    [Fact]
    public async Task HandleAsync_WithValidRequest_ShouldSucceed()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
        var inviterUserId = UserId.New();
        var targetUserId = UserId.New();
        var request = new InviteMemberRequest(targetUserId.ToString());

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, inviterUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        _guildMemberRepositoryMock
            .Setup(x => x.GetInviteMemberTargetLookupAsync(guild.Id, targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InviteMemberTargetLookup(UserExists: true, IsMember: false));

        _guildMemberRepositoryMock
            .Setup(x => x.TryAddAsync(It.IsAny<GuildMember>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var response = await _handler.HandleAsync(new InviteMemberInput(guild.Id, request.UserId), inviterUserId);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();
        response.Data!.GuildId.Should().Be(guild.Id.ToString());
        response.Data.UserId.Should().Be(targetUserId.ToString());
        response.Data.Role.Should().Be(GuildRole.Member.ToString());
    }

}
