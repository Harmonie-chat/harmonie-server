using FluentAssertions;
using Harmonie.Domain.Entities;
using Harmonie.Domain.ValueObjects;
using Xunit;

namespace Harmonie.Domain.Tests;

public sealed class UserTests
{
    [Fact]
    public void Create_WithValidData_ShouldSucceed()
    {
        // Arrange
        var email = Email.Create("test@harmonie.chat").Value!;
        var username = Username.Create("testuser").Value!;
        var passwordHash = "hashed_password";

        // Act
        var result = User.Create(email, username, passwordHash);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Email.Should().Be(email);
        result.Value.Username.Should().Be(username);
        result.Value.IsActive.Should().BeTrue();
        result.Value.IsEmailVerified.Should().BeFalse();
    }

    [Fact]
    public void UpdateEmail_ShouldSetEmailVerifiedToFalse()
    {
        // Arrange
        var user = User.Create(
            Email.Create("old@harmonie.chat").Value!,
            Username.Create("testuser").Value!,
            "hash").Value!;
        user.VerifyEmail();
        var newEmail = Email.Create("new@harmonie.chat").Value!;

        // Act
        user.UpdateEmail(newEmail);

        // Assert
        user.Email.Should().Be(newEmail);
        user.IsEmailVerified.Should().BeFalse();
    }

    [Fact]
    public void UpdateDisplayName_WithNull_ShouldClearDisplayName()
    {
        // Arrange
        var user = User.Create(
            Email.Create("user-display@harmonie.chat").Value!,
            Username.Create("displayuser").Value!,
            "hash").Value!;
        user.UpdateDisplayName("Alice");

        // Act
        var result = user.UpdateDisplayName(null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.DisplayName.Should().BeNull();
    }

    [Fact]
    public void UpdateBio_WithTooLongValue_ShouldFail()
    {
        // Arrange
        var user = User.Create(
            Email.Create("user-bio@harmonie.chat").Value!,
            Username.Create("biouser").Value!,
            "hash").Value!;
        var tooLongBio = new string('b', 501);

        // Act
        var result = user.UpdateBio(tooLongBio);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Bio is too long");
    }

    [Fact]
    public void UpdateAvatar_WithTooLongValue_ShouldFail()
    {
        // Arrange
        var user = User.Create(
            Email.Create("user-avatar@harmonie.chat").Value!,
            Username.Create("avataruser").Value!,
            "hash").Value!;
        var tooLongAvatarUrl = $"https://cdn.harmonie.chat/{new string('a', 2100)}";

        // Act
        var result = user.UpdateAvatar(tooLongAvatarUrl);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Avatar URL is too long");
    }
}
