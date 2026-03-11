using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Users.GetMyProfile;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests;

public sealed class GetMyProfileHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly GetMyProfileHandler _handler;

    public GetMyProfileHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _handler = new GetMyProfileHandler(
            _userRepositoryMock.Object,
            NullLogger<GetMyProfileHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WhenUserExists_ShouldReturnProfile()
    {
        var user = CreateUser();
        user.UpdateDisplayName("Alice");
        user.UpdateBio("Hello Harmonie");
        var avatarFileId = UploadedFileId.New();
        user.UpdateAvatarFile(avatarFileId);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var response = await _handler.HandleAsync(user.Id);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();
        response.Data!.UserId.Should().Be(user.Id.ToString());
        response.Data.Username.Should().Be(user.Username.Value);
        response.Data.DisplayName.Should().Be("Alice");
        response.Data.Bio.Should().Be("Hello Harmonie");
        response.Data.AvatarFileId.Should().Be(avatarFileId.ToString());
        response.Data.Theme.Should().Be("default");
        response.Data.Language.Should().BeNull();
        response.Data.Avatar.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_WhenUserDoesNotExist_ShouldReturnNotFound()
    {
        var userId = UserId.New();

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var response = await _handler.HandleAsync(userId);

        response.Success.Should().BeFalse();
        response.Data.Should().BeNull();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.User.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenUserHasAvatarAppearance_ShouldReturnAvatarObject()
    {
        var user = CreateUser();
        user.UpdateAvatarColor("#FFF4D6");
        user.UpdateAvatarIcon("star");
        user.UpdateAvatarBg("#1F2937");

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var response = await _handler.HandleAsync(user.Id);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Avatar.Should().NotBeNull();
        response.Data.Avatar!.Color.Should().Be("#FFF4D6");
        response.Data.Avatar.Icon.Should().Be("star");
        response.Data.Avatar.Bg.Should().Be("#1F2937");
    }

    [Fact]
    public async Task HandleAsync_WhenUserHasThemeAndLanguage_ShouldReturnThem()
    {
        var user = CreateUser();
        user.UpdateTheme("dark");
        user.UpdateLanguage("fr");

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var response = await _handler.HandleAsync(user.Id);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Theme.Should().Be("dark");
        response.Data.Language.Should().Be("fr");
    }

    private static User CreateUser()
    {
        var emailResult = Email.Create($"test-{Guid.NewGuid():N}@harmonie.chat");
        if (emailResult.IsFailure || emailResult.Value is null)
            throw new InvalidOperationException("Failed to create email for tests.");

        var usernameResult = Username.Create($"user{Guid.NewGuid():N}"[..20]);
        if (usernameResult.IsFailure || usernameResult.Value is null)
            throw new InvalidOperationException("Failed to create username for tests.");

        var userResult = User.Create(
            emailResult.Value,
            usernameResult.Value,
            "hashed_password");
        if (userResult.IsFailure || userResult.Value is null)
            throw new InvalidOperationException("Failed to create user for tests.");

        return userResult.Value;
    }
}
