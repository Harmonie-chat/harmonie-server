using FluentAssertions;
using Harmonie.Domain.Entities.Users;
using Harmonie.Domain.ValueObjects.Common;
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
        var result = User.Create(email, username, passwordHash, TestTime.UtcNow);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Email.Should().Be(email);
        result.Value.Username.Should().Be(username);
        result.Value.IsActive.Should().BeTrue();
        result.Value.IsEmailVerified.Should().BeFalse();
        result.Value.Theme.Should().Be("default");
        result.Value.Language.Should().BeNull();
        result.Value.CreatedAtUtc.Should().Be(TestTime.UtcNow);
        result.Value.UpdatedAtUtc.Should().Be(TestTime.UtcNow);
    }

    [Fact]
    public void UpdateEmail_ShouldSetEmailVerifiedToFalse()
    {
        // Arrange
        var user = User.Create(
            Email.Create("old@harmonie.chat").Value!,
            Username.Create("testuser").Value!,
            "hash",
            TestTime.UtcNow).Value!;
        user.VerifyEmail(TestTime.UtcNow);
        var newEmail = Email.Create("new@harmonie.chat").Value!;

        // Act
        user.UpdateEmail(newEmail, TestTime.UtcNow);

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
            "hash",
            TestTime.UtcNow).Value!;
        user.UpdateDisplayName("Alice", TestTime.UtcNow);

        // Act
        var result = user.UpdateDisplayName(null, TestTime.UtcNow);

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
            "hash",
            TestTime.UtcNow).Value!;
        var tooLongBio = new string('b', 501);

        // Act
        var result = user.UpdateBio(tooLongBio, TestTime.UtcNow);

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
            "hash",
            TestTime.UtcNow).Value!;
        var avatarFileId = UploadedFileId.New();

        var result = user.UpdateAvatarFile(avatarFileId, TestTime.UtcNow);

        result.IsSuccess.Should().BeTrue();
        user.AvatarFileId.Should().Be(avatarFileId);
    }

    [Fact]
    public void UpdateTheme_WithValidValue_ShouldSucceed()
    {
        var user = CreateUser();

        var result = user.UpdateTheme("dark", TestTime.UtcNow);

        result.IsSuccess.Should().BeTrue();
        user.Theme.Should().Be("dark");
    }

    [Fact]
    public void UpdateTheme_WithEmptyValue_ShouldFail()
    {
        var user = CreateUser();

        var result = user.UpdateTheme("", TestTime.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Theme cannot be empty");
    }

    [Fact]
    public void UpdateTheme_WithTooLongValue_ShouldFail()
    {
        var user = CreateUser();

        var result = user.UpdateTheme(new string('t', 51), TestTime.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Theme is too long");
    }

    [Fact]
    public void UpdateLanguage_WithNull_ShouldClear()
    {
        var user = CreateUser();
        user.UpdateLanguage("fr", TestTime.UtcNow);

        var result = user.UpdateLanguage(null, TestTime.UtcNow);

        result.IsSuccess.Should().BeTrue();
        user.Language.Should().BeNull();
    }

    [Fact]
    public void UpdateLanguage_WithTooLongValue_ShouldFail()
    {
        var user = CreateUser();

        var result = user.UpdateLanguage("toolongvalue", TestTime.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Language is too long");
    }

    [Fact]
    public void UpdateAvatar_WithValidAppearance_ShouldSucceed()
    {
        var user = CreateUser();
        var appearance = Appearance.Create("#FFF4D6", "star", "#000").Value!;

        var result = user.UpdateAvatar(appearance, TestTime.UtcNow);

        result.IsSuccess.Should().BeTrue();
        user.Avatar.Color.Should().Be("#FFF4D6");
        user.Avatar.Glyph.Should().Be("star");
        user.Avatar.Bg.Should().Be("#000");
    }

    [Fact]
    public void UpdateAvatar_WithEmpty_ShouldClear()
    {
        var user = CreateUser();
        user.UpdateAvatar(Appearance.Create("#FFF4D6", null, null).Value!, TestTime.UtcNow);

        var result = user.UpdateAvatar(Appearance.Empty, TestTime.UtcNow);

        result.IsSuccess.Should().BeTrue();
        user.Avatar.HasValue.Should().BeFalse();
    }

    [Theory]
    [InlineData("idle")]
    [InlineData("dnd")]
    [InlineData("invisible")]
    public void UpdateStatus_WithValidValue_ShouldSucceed(string status)
    {
        var user = CreateUser();
        var userStatus = UserStatus.Create(status).Value!;

        var result = user.UpdateStatus(userStatus, TestTime.UtcNow);

        result.IsSuccess.Should().BeTrue();
        user.Status.Value.Should().Be(status);
        user.StatusUpdatedAtUtc.Should().Be(TestTime.UtcNow);
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

        var result = user.UpdateStatus(UserStatus.Online, TestTime.UtcNow);

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
            avatar: Appearance.Empty,
            theme: "dark",
            language: null,
            status: UserStatus.Idle,
            statusUpdatedAtUtc: null,
            createdAtUtc: TestTime.UtcNow,
            updatedAtUtc: null);

        user.Status.Should().Be(UserStatus.Idle);
        user.Theme.Should().Be("dark");
    }

    [Fact]
    public void UpdateStatus_FromIdleToOnline_ShouldUpdateTimestamp()
    {
        var user = CreateUser();
        user.UpdateStatus(UserStatus.Idle, TestTime.UtcNow);
        var idleTimestamp = user.StatusUpdatedAtUtc;

        user.UpdateStatus(UserStatus.Online, TestTime.UtcNow.AddMinutes(1));

        user.Status.Should().Be(UserStatus.Online);
        user.StatusUpdatedAtUtc.Should().NotBeNull();
        user.StatusUpdatedAtUtc.Should().BeAfter(idleTimestamp!.Value);
    }

    private static User CreateUser()
    {
        return User.Create(
            Email.Create("test@harmonie.chat").Value!,
            Username.Create("testuser").Value!,
            "hash",
            TestTime.UtcNow).Value!;
    }
}
