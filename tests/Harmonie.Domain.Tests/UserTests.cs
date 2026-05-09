using FluentAssertions;
using Harmonie.Domain.Entities.Users;
using Harmonie.Domain.ValueObjects.Uploads;
using Harmonie.Domain.ValueObjects.Users;
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
        result.Value.Theme.Should().Be("default");
        result.Value.Language.Should().BeNull();
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
    public void UpdateAvatarFile_WithValue_ShouldSucceed()
    {
        var user = User.Create(
            Email.Create("user-avatar@harmonie.chat").Value!,
            Username.Create("avataruser").Value!,
            "hash").Value!;
        var avatarFileId = UploadedFileId.New();

        var result = user.UpdateAvatarFile(avatarFileId);

        result.IsSuccess.Should().BeTrue();
        user.AvatarFileId.Should().Be(avatarFileId);
    }

    [Fact]
    public void UpdateTheme_WithValidValue_ShouldSucceed()
    {
        var user = CreateUser();

        var result = user.UpdateTheme("dark");

        result.IsSuccess.Should().BeTrue();
        user.Theme.Should().Be("dark");
    }

    [Fact]
    public void UpdateTheme_WithEmptyValue_ShouldFail()
    {
        var user = CreateUser();

        var result = user.UpdateTheme("");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Theme cannot be empty");
    }

    [Fact]
    public void UpdateTheme_WithTooLongValue_ShouldFail()
    {
        var user = CreateUser();

        var result = user.UpdateTheme(new string('t', 51));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Theme is too long");
    }

    [Fact]
    public void UpdateLanguage_WithNull_ShouldClear()
    {
        var user = CreateUser();
        user.UpdateLanguage("fr");

        var result = user.UpdateLanguage(null);

        result.IsSuccess.Should().BeTrue();
        user.Language.Should().BeNull();
    }

    [Fact]
    public void UpdateLanguage_WithTooLongValue_ShouldFail()
    {
        var user = CreateUser();

        var result = user.UpdateLanguage("toolongvalue");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Language is too long");
    }

    [Fact]
    public void UpdateAvatarColor_WithValidValue_ShouldSucceed()
    {
        var user = CreateUser();

        var result = user.UpdateAvatarColor("#FFF4D6");

        result.IsSuccess.Should().BeTrue();
        user.AvatarColor.Should().Be("#FFF4D6");
    }

    [Fact]
    public void UpdateAvatarColor_WithNull_ShouldClear()
    {
        var user = CreateUser();
        user.UpdateAvatarColor("#FFF4D6");

        var result = user.UpdateAvatarColor(null);

        result.IsSuccess.Should().BeTrue();
        user.AvatarColor.Should().BeNull();
    }

    [Fact]
    public void UpdateAvatarColor_WithTooLongValue_ShouldFail()
    {
        var user = CreateUser();

        var result = user.UpdateAvatarColor(new string('c', 51));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Avatar color is too long");
    }

    [Fact]
    public void UpdateAvatarIcon_WithTooLongValue_ShouldFail()
    {
        var user = CreateUser();

        var result = user.UpdateAvatarIcon(new string('i', 51));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Avatar icon is too long");
    }

    [Fact]
    public void UpdateAvatarBg_WithTooLongValue_ShouldFail()
    {
        var user = CreateUser();

        var result = user.UpdateAvatarBg(new string('b', 51));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Avatar background is too long");
    }

    [Theory]
    [InlineData("idle")]
    [InlineData("dnd")]
    [InlineData("invisible")]
    public void UpdateStatus_WithValidValue_ShouldSucceed(string status)
    {
        var user = CreateUser();
        var userStatus = UserStatus.Create(status).Value!;

        var result = user.UpdateStatus(userStatus);

        result.IsSuccess.Should().BeTrue();
        user.Status.Value.Should().Be(status);
        user.StatusUpdatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void UpdateStatus_DefaultValue_ShouldBeOnline()
    {
        var user = CreateUser();

        user.Status.Should().Be(UserStatus.Online);
        user.StatusUpdatedAtUtc.Should().BeNull();
    }

    [Fact]
    public void UpdateStatus_SameStatus_ShouldBeNoop()
    {
        var user = CreateUser();
        var updatedAtBefore = user.StatusUpdatedAtUtc;

        var result = user.UpdateStatus(UserStatus.Online);

        result.IsSuccess.Should().BeTrue();
        user.StatusUpdatedAtUtc.Should().Be(updatedAtBefore);
    }

    [Fact]
    public void Rehydrate_WithExplicitStatus_ShouldSetStatus()
    {
        var user = User.Rehydrate(
            UserId.New(),
            Email.Create("rehydrate@harmonie.chat").Value!,
            Username.Create("rehydrate").Value!,
            "hash",
            avatarFileId: null,
            isEmailVerified: true,
            isActive: true,
            lastLoginAtUtc: null,
            displayName: null,
            bio: null,
            avatarColor: null,
            avatarIcon: null,
            avatarBg: null,
            theme: "dark",
            language: null,
            status: UserStatus.Idle,
            statusUpdatedAtUtc: null,
            createdAtUtc: DateTime.UtcNow,
            updatedAtUtc: null);

        user.Status.Should().Be(UserStatus.Idle);
        user.Theme.Should().Be("dark");
    }

    [Fact]
    public void UpdateStatus_FromIdleToOnline_ShouldUpdateTimestamp()
    {
        var user = CreateUser();
        user.UpdateStatus(UserStatus.Idle);
        var idleTimestamp = user.StatusUpdatedAtUtc;

        user.UpdateStatus(UserStatus.Online);

        user.Status.Should().Be(UserStatus.Online);
        user.StatusUpdatedAtUtc.Should().NotBeNull();
        user.StatusUpdatedAtUtc.Should().BeAfter(idleTimestamp!.Value);
    }

    private static User CreateUser()
    {
        return User.Create(
            Email.Create("test@harmonie.chat").Value!,
            Username.Create("testuser").Value!,
            "hash").Value!;
    }
}
