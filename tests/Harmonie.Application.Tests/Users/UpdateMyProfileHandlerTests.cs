using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Common.Uploads;
using Harmonie.Application.Features.Users.UpdateMyProfile;
using Harmonie.Application.Interfaces.Uploads;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Domain.ValueObjects.Common;
using Harmonie.Application.Tests.Common;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Uploads;
using Harmonie.Domain.Entities.Users;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Users;

public sealed class UpdateMyProfileHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IUploadedFileRepository> _uploadedFileRepositoryMock;
    private readonly Mock<IObjectStorageService> _objectStorageServiceMock;
    private readonly Mock<IUserProfileNotifier> _userProfileNotifierMock;
    private readonly UpdateMyProfileHandler _handler;

    public UpdateMyProfileHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _uploadedFileRepositoryMock = new Mock<IUploadedFileRepository>();
        _objectStorageServiceMock = new Mock<IObjectStorageService>();
        _userProfileNotifierMock = new Mock<IUserProfileNotifier>();

        _userRepositoryMock
            .Setup(x => x.GetUserNotificationContextAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserNotificationContext(Array.Empty<GuildId>(), Array.Empty<ConversationId>()));

        _handler = new UpdateMyProfileHandler(
            _userRepositoryMock.Object,
            new UploadedFileCleanupService(
                _uploadedFileRepositoryMock.Object,
                _objectStorageServiceMock.Object,
                NullLogger<UploadedFileCleanupService>.Instance),
            _userProfileNotifierMock.Object,
            TestTime.CreateProvider(),
            NullLogger<UpdateMyProfileHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WhenUserExistsAndRequestIsPartial_ShouldUpdateOnlyProvidedField()
    {
        var user = ApplicationTestBuilders.CreateUser();
        user.UpdateDisplayName("Initial Name", TestTime.UtcNow);
        user.UpdateBio("Initial bio", TestTime.UtcNow);
        var avatarFileId = UploadedFileId.New();
        user.UpdateAvatarFile(avatarFileId, TestTime.UtcNow);

        var request = new UpdateMyProfileRequest
        {
            DisplayName = "Updated Name",
            DisplayNameIsSet = true,
            BioIsSet = false,
            AvatarFileIdIsSet = false
        };

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var response = await _handler.HandleAsync(request, user.Id, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();
        response.Data!.DisplayName.Should().Be("Updated Name");
        response.Data.Bio.Should().Be("Initial bio");
        response.Data.AvatarFileId.Should().Be(avatarFileId.Value);

        _userRepositoryMock.Verify(
            x => x.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenFieldIsExplicitlyNull_ShouldResetToNull()
    {
        var user = ApplicationTestBuilders.CreateUser();
        user.UpdateDisplayName("Alice", TestTime.UtcNow);
        user.UpdateBio("Existing bio", TestTime.UtcNow);
        user.UpdateAvatarFile(UploadedFileId.New(), TestTime.UtcNow);

        var request = new UpdateMyProfileRequest
        {
            Bio = null,
            AvatarFileId = null,
            DisplayNameIsSet = false,
            BioIsSet = true,
            AvatarFileIdIsSet = true
        };

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var response = await _handler.HandleAsync(request, user.Id, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();
        response.Data!.DisplayName.Should().Be("Alice");
        response.Data.Bio.Should().BeNull();
        response.Data.AvatarFileId.Should().BeNull();

        _userRepositoryMock.Verify(
            x => x.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenDomainInvariantFails_ShouldReturnStableValidationFailure()
    {
        var user = ApplicationTestBuilders.CreateUser();
        var request = new UpdateMyProfileRequest
        {
            DisplayName = new string('x', 101),
            DisplayNameIsSet = true
        };

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var response = await _handler.HandleAsync(request, user.Id, TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Data.Should().BeNull();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
        response.Error.Errors.Should().NotBeNull();
        response.Error.Errors!.Should().ContainKey(nameof(request.DisplayName));

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

        var response = await _handler.HandleAsync(request, userId, TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Data.Should().BeNull();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.User.NotFound);

        _userRepositoryMock.Verify(
            x => x.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenThemeIsSet_ShouldUpdateTheme()
    {
        var user = ApplicationTestBuilders.CreateUser();
        var request = new UpdateMyProfileRequest
        {
            Theme = "dark",
            ThemeIsSet = true
        };

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var response = await _handler.HandleAsync(request, user.Id, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Theme.Should().Be("dark");

        _userRepositoryMock.Verify(
            x => x.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenLanguageIsExplicitlyNull_ShouldClearLanguage()
    {
        var user = ApplicationTestBuilders.CreateUser();
        user.UpdateLanguage("fr", TestTime.UtcNow);

        var request = new UpdateMyProfileRequest
        {
            Language = null,
            LanguageIsSet = true
        };

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var response = await _handler.HandleAsync(request, user.Id, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Language.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_WhenAvatarAppearanceIsSet_ShouldUpdateAvatarFields()
    {
        var user = ApplicationTestBuilders.CreateUser();
        var request = new UpdateMyProfileRequest
        {
            AvatarIsSet = true,
            AvatarColor = "#FFF4D6",
            AvatarColorIsSet = true,
            AvatarIcon = "star",
            AvatarIconIsSet = true,
            AvatarBg = "#1F2937",
            AvatarBgIsSet = true
        };

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var response = await _handler.HandleAsync(request, user.Id, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Avatar.Should().NotBeNull();
        response.Data.Avatar!.Color.Should().Be("#FFF4D6");
        response.Data.Avatar.Icon.Should().Be("star");
        response.Data.Avatar.Bg.Should().Be("#1F2937");
    }

    [Fact]
    public async Task HandleAsync_WhenAvatarAppearanceIsPartial_ShouldOnlyUpdateProvidedSubFields()
    {
        var user = ApplicationTestBuilders.CreateUser();
        user.UpdateAvatar(
            Appearance.Create("#INITIAL", "heart", "#000000").Value!,
            TestTime.UtcNow);

        var request = new UpdateMyProfileRequest
        {
            AvatarIsSet = true,
            AvatarColor = "#UPDATED",
            AvatarColorIsSet = true,
            AvatarIconIsSet = false,
            AvatarBgIsSet = false
        };

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var response = await _handler.HandleAsync(request, user.Id, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Avatar.Should().NotBeNull();
        response.Data.Avatar!.Color.Should().Be("#UPDATED");
        response.Data.Avatar.Icon.Should().Be("heart");
        response.Data.Avatar.Bg.Should().Be("#000000");

        _userRepositoryMock.Verify(
            x => x.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(nameof(UpdateMyProfileRequest.DisplayNameIsSet))]
    [InlineData(nameof(UpdateMyProfileRequest.BioIsSet))]
    [InlineData(nameof(UpdateMyProfileRequest.AvatarFileIdIsSet))]
    [InlineData(nameof(UpdateMyProfileRequest.AvatarColorIsSet))]
    [InlineData(nameof(UpdateMyProfileRequest.AvatarIconIsSet))]
    [InlineData(nameof(UpdateMyProfileRequest.AvatarBgIsSet))]
    public async Task HandleAsync_WhenVisibleFieldIsSet_ShouldNotifyProfileUpdate(string fieldName)
    {
        var user = ApplicationTestBuilders.CreateUser();
        var request = new UpdateMyProfileRequest();
        typeof(UpdateMyProfileRequest).GetProperty(fieldName)!.SetValue(request, true);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        await _handler.HandleAsync(request, user.Id, TestContext.Current.CancellationToken);

        _userProfileNotifierMock.Verify(
            x => x.NotifyProfileUpdatedAsync(
                It.Is<UserProfileUpdatedNotification>(n => n.UserId == user.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("dark", true, false)]
    [InlineData(null, false, true)]
    public async Task HandleAsync_WhenOnlyPersonalSettingIsSet_ShouldNotNotify(
        string? theme, bool themeIsSet, bool languageIsSet)
    {
        var user = ApplicationTestBuilders.CreateUser();
        var request = new UpdateMyProfileRequest
        {
            Theme = theme,
            ThemeIsSet = themeIsSet,
            LanguageIsSet = languageIsSet
        };

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        await _handler.HandleAsync(request, user.Id, TestContext.Current.CancellationToken);

        _userProfileNotifierMock.Verify(
            x => x.NotifyProfileUpdatedAsync(It.IsAny<UserProfileUpdatedNotification>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenNoFieldIsSet_ShouldNotNotify()
    {
        var user = ApplicationTestBuilders.CreateUser();
        var request = new UpdateMyProfileRequest();

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        await _handler.HandleAsync(request, user.Id, TestContext.Current.CancellationToken);

        _userProfileNotifierMock.Verify(
            x => x.NotifyProfileUpdatedAsync(It.IsAny<UserProfileUpdatedNotification>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenNoFieldIsSet_ShouldNotCallRepository()
    {
        var user = ApplicationTestBuilders.CreateUser();
        var request = new UpdateMyProfileRequest();

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var response = await _handler.HandleAsync(request, user.Id, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();

        _userRepositoryMock.Verify(
            x => x.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenAllAvatarFieldsAreNull_ShouldReturnNullAvatar()
    {
        var user = ApplicationTestBuilders.CreateUser();
        var request = new UpdateMyProfileRequest();

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var response = await _handler.HandleAsync(request, user.Id, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Avatar.Should().BeNull();
        response.Data.Theme.Should().Be("default");
        response.Data.Language.Should().BeNull();
    }

}
