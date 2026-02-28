using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Users.UpdateMyProfile;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests;

public sealed class UpdateMyProfileHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly UpdateMyProfileHandler _handler;

    public UpdateMyProfileHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _handler = new UpdateMyProfileHandler(
            _userRepositoryMock.Object,
            NullLogger<UpdateMyProfileHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WhenUserExistsAndRequestIsPartial_ShouldUpdateOnlyProvidedField()
    {
        var user = CreateUser();
        user.UpdateDisplayName("Initial Name");
        user.UpdateBio("Initial bio");
        user.UpdateAvatar("https://cdn.harmonie.chat/avatar-initial.png");

        var request = new UpdateMyProfileRequest
        {
            DisplayName = "Updated Name",
            DisplayNameIsSet = true,
            BioIsSet = false,
            AvatarUrlIsSet = false
        };

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var response = await _handler.HandleAsync(request, user.Id);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();
        response.Data!.DisplayName.Should().Be("Updated Name");
        response.Data.Bio.Should().Be("Initial bio");
        response.Data.AvatarUrl.Should().Be("https://cdn.harmonie.chat/avatar-initial.png");

        _userRepositoryMock.Verify(
            x => x.UpdateAsync(user, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenFieldIsExplicitlyNull_ShouldResetToNull()
    {
        var user = CreateUser();
        user.UpdateDisplayName("Alice");
        user.UpdateBio("Existing bio");
        user.UpdateAvatar("https://cdn.harmonie.chat/avatar-existing.png");

        var request = new UpdateMyProfileRequest
        {
            Bio = null,
            AvatarUrl = null,
            DisplayNameIsSet = false,
            BioIsSet = true,
            AvatarUrlIsSet = true
        };

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var response = await _handler.HandleAsync(request, user.Id);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();
        response.Data!.DisplayName.Should().Be("Alice");
        response.Data.Bio.Should().BeNull();
        response.Data.AvatarUrl.Should().BeNull();

        _userRepositoryMock.Verify(
            x => x.UpdateAsync(user, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenDomainInvariantFails_ShouldReturnStableValidationFailure()
    {
        var user = CreateUser();
        var request = new UpdateMyProfileRequest
        {
            DisplayName = new string('x', 101),
            DisplayNameIsSet = true
        };

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var response = await _handler.HandleAsync(request, user.Id);

        response.Success.Should().BeFalse();
        response.Data.Should().BeNull();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
        response.Error.Details.Should().NotBeNull();
        response.Error.Details!.Should().ContainKey(nameof(request.DisplayName));

        _userRepositoryMock.Verify(
            x => x.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenUserDoesNotExist_ShouldReturnNotFound()
    {
        var userId = UserId.New();
        var request = new UpdateMyProfileRequest
        {
            DisplayName = "Alice",
            DisplayNameIsSet = true
        };

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var response = await _handler.HandleAsync(request, userId);

        response.Success.Should().BeFalse();
        response.Data.Should().BeNull();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.User.NotFound);

        _userRepositoryMock.Verify(
            x => x.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
