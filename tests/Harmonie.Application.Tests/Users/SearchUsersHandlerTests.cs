using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Users.SearchUsers;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Application.Tests.Common;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Uploads;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Users;

public sealed class SearchUsersHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IGuildRepository> _guildRepositoryMock;
    private readonly SearchUsersHandler _handler;

    public SearchUsersHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _guildRepositoryMock = new Mock<IGuildRepository>();

        _handler = new SearchUsersHandler(
            _userRepositoryMock.Object,
            _guildRepositoryMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WhenGuildScopeGuildDoesNotExist_ShouldReturnNotFound()
    {
        var currentUserId = UserId.New();
        var guildId = GuildId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guildId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildAccessContext?)null);

        var response = await _handler.HandleAsync(
            new SearchUsersRequest
            {
                Q = "alice",
                GuildId = guildId
            },
            currentUserId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenGuildScopeUserIsNotMember_ShouldReturnAccessDenied()
    {
        var ownerId = UserId.New();
        var currentUserId = UserId.New();
        var guild = ApplicationTestBuilders.CreateGuild(ownerId);

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, CallerRole: null));

        var response = await _handler.HandleAsync(
            new SearchUsersRequest
            {
                Q = "alice",
                GuildId = guild.Id
            },
            currentUserId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WithValidRequest_ShouldReturnMappedUsers()
    {
        var ownerId = UserId.New();
        var guild = ApplicationTestBuilders.CreateGuild(ownerId);
        var matchedUser = CreateSearchUser("alice-dev", "Alice Dev", isActive: true);

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        _userRepositoryMock
            .Setup(x => x.SearchUsersAsync(
                It.Is<SearchUsersQuery>(query =>
                    query.SearchText == "Alice"
                    && query.GuildId == guild.Id
                    && query.Limit == 5),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([matchedUser]);

        var response = await _handler.HandleAsync(
            new SearchUsersRequest
            {
                Q = " Alice ",
                GuildId = guild.Id,
                Limit = 5
            },
            ownerId);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Users.Should().ContainSingle();
        response.Data.Users[0].UserId.Should().Be(matchedUser.UserId.Value);
        response.Data.Users[0].Username.Should().Be("alice-dev");
        response.Data.Users[0].DisplayName.Should().Be("Alice Dev");
        response.Data.Users[0].Status.Should().Be("Active");
    }

    private static SearchUserResult CreateSearchUser(string username, string? displayName, bool isActive)
    {
        var usernameResult = Username.Create(username);
        if (usernameResult.IsFailure || usernameResult.Value is null)
            throw new InvalidOperationException("Failed to create test username.");

        return new SearchUserResult(
            UserId: UserId.New(),
            Username: usernameResult.Value,
            DisplayName: displayName,
            AvatarFileId: UploadedFileId.From(Guid.Parse("9b46d971-3590-4f09-bce6-2a218fc8a8ec")),
            Bio: null,
            IsActive: isActive);
    }
}
