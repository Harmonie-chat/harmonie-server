using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Users.GetMyProfile;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Domain.ValueObjects.Common;
using Harmonie.Application.Tests.Common;
using Harmonie.Domain.ValueObjects.Uploads;
using Harmonie.Domain.Entities.Users;
using Harmonie.Domain.ValueObjects.Users;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Users;

public sealed class GetMyProfileHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly GetMyProfileHandler _handler;

    public GetMyProfileHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _handler = new GetMyProfileHandler(
            _userRepositoryMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WhenUserExists_ShouldReturnProfile()
    {
        var user = ApplicationTestBuilders.CreateUser();
        user.UpdateDisplayName("Alice", TestClock.UtcNow);
        user.UpdateBio("Hello Harmonie", TestClock.UtcNow);
        var avatarFileId = UploadedFileId.New();
        user.UpdateAvatarFile(avatarFileId, TestClock.UtcNow);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var response = await _handler.HandleAsync(Unit.Value, user.Id, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();
        response.Data!.UserId.Should().Be(user.Id.Value);
        response.Data.Username.Should().Be(user.Username.Value);
        response.Data.DisplayName.Should().Be("Alice");
        response.Data.Bio.Should().Be("Hello Harmonie");
        response.Data.AvatarFileId.Should().Be(avatarFileId.Value);
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

        var response = await _handler.HandleAsync(Unit.Value, userId, TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Data.Should().BeNull();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.User.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenUserHasAvatarAppearance_ShouldReturnAvatarObject()
    {
        var user = ApplicationTestBuilders.CreateUser();
        user.UpdateAvatar(
            Appearance.Create("#FFF4D6", "star", "#1F2937").Value!,
            TestClock.UtcNow);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var response = await _handler.HandleAsync(Unit.Value, user.Id, TestContext.Current.CancellationToken);

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
        var user = ApplicationTestBuilders.CreateUser();
        user.UpdateTheme("dark", TestClock.UtcNow);
        user.UpdateLanguage("fr", TestClock.UtcNow);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var response = await _handler.HandleAsync(Unit.Value, user.Id, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Theme.Should().Be("dark");
        response.Data.Language.Should().Be("fr");
    }

}
