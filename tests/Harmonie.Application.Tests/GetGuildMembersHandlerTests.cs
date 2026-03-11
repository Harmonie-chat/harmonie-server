using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Guilds.GetGuildMembers;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests;

public sealed class GetGuildMembersHandlerTests
{
    private readonly Mock<IGuildRepository> _guildRepositoryMock;
    private readonly Mock<IGuildMemberRepository> _guildMemberRepositoryMock;
    private readonly GetGuildMembersHandler _handler;

    public GetGuildMembersHandlerTests()
    {
        _guildRepositoryMock = new Mock<IGuildRepository>();
        _guildMemberRepositoryMock = new Mock<IGuildMemberRepository>();
        _handler = new GetGuildMembersHandler(
            _guildRepositoryMock.Object,
            _guildMemberRepositoryMock.Object,
            NullLogger<GetGuildMembersHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WhenGuildDoesNotExist_ShouldReturnNotFound()
    {
        var guildId = GuildId.New();
        var requesterUserId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guildId, requesterUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildAccessContext?)null);

        var response = await _handler.HandleAsync(guildId, requesterUserId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenRequesterIsNotMember_ShouldReturnAccessDenied()
    {
        var guild = CreateGuild();
        var requesterUserId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, requesterUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, null));

        var response = await _handler.HandleAsync(guild.Id, requesterUserId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WhenRequesterIsMember_ShouldReturnGuildMembers()
    {
        var guild = CreateGuild();
        var requesterUserId = UserId.New();
        var adminUser = CreateMemberUser(GuildRole.Admin, "owner", displayName: "Owner");
        var memberUser = CreateMemberUser(GuildRole.Member, "member", displayName: null);

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, requesterUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Member));

        _guildMemberRepositoryMock
            .Setup(x => x.GetGuildMembersAsync(guild.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([adminUser, memberUser]);

        var response = await _handler.HandleAsync(guild.Id, requesterUserId);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();
        response.Data!.GuildId.Should().Be(guild.Id.ToString());
        response.Data.Members.Should().HaveCount(2);
        response.Data.Members[0].Role.Should().Be("Admin");
        response.Data.Members[0].DisplayName.Should().Be("Owner");
        response.Data.Members[1].Role.Should().Be("Member");
        response.Data.Members[1].DisplayName.Should().BeNull();
    }

    private static Guild CreateGuild()
    {
        var guildNameResult = GuildName.Create("Guild Alpha");
        if (guildNameResult.IsFailure || guildNameResult.Value is null)
            throw new InvalidOperationException("Failed to create guild name for tests.");

        return Guild.Rehydrate(
            GuildId.New(),
            guildNameResult.Value,
            UserId.New(),
            DateTime.UtcNow.AddDays(-2),
            DateTime.UtcNow.AddDays(-1));
    }

    private static GuildMemberUser CreateMemberUser(
        GuildRole role,
        string usernameValue,
        string? displayName)
    {
        var usernameResult = Username.Create(usernameValue);
        if (usernameResult.IsFailure || usernameResult.Value is null)
            throw new InvalidOperationException("Failed to create username for tests.");

        return new GuildMemberUser(
            UserId.New(),
            usernameResult.Value,
            displayName,
            AvatarFileId: null,
            IsActive: true,
            Role: role,
            DateTime.UtcNow.AddDays(-1));
    }
}
