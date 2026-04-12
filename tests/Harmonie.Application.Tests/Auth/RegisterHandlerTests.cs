using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Tests.Common;
using Harmonie.Application.Features.Auth.Register;
using Harmonie.Application.Features.Users;
using Harmonie.Application.Interfaces.Auth;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Domain.Entities.Users;
using Harmonie.Domain.ValueObjects.Users;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Auth;

/// <summary>
/// Tests for RegisterHandler
/// </summary>
public sealed class RegisterHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly Mock<IPasswordHasher> _passwordHasherMock;
    private readonly Mock<IJwtTokenService> _jwtTokenServiceMock;
    private readonly RegisterHandler _handler;

    public RegisterHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _refreshTokenRepositoryMock = new Mock<IRefreshTokenRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = new Mock<IUnitOfWorkTransaction>();
        _passwordHasherMock = new Mock<IPasswordHasher>();
        _jwtTokenServiceMock = new Mock<IJwtTokenService>();

        _transactionMock = _unitOfWorkMock.SetupTransactionMock();

        _handler = new RegisterHandler(
            _userRepositoryMock.Object,
            _refreshTokenRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _passwordHasherMock.Object,
            _jwtTokenServiceMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WithValidRequest_ShouldSucceed()
    {
        // Arrange
        var request = new RegisterRequest(
            "test@harmonie.chat",
            "testuser",
            "Test123!@#");

        _userRepositoryMock
            .Setup(x => x.CheckDuplicatesAsync(It.IsAny<Email>(), It.IsAny<Username>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserDuplicateCheck(false, false));

        _passwordHasherMock
            .Setup(x => x.HashPassword(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("hashed_password");

        _jwtTokenServiceMock
            .Setup(x => x.GenerateAccessToken(It.IsAny<UserId>(), It.IsAny<Email>(), It.IsAny<Username>()))
            .Returns("access_token");

        _jwtTokenServiceMock
            .Setup(x => x.GenerateRefreshToken())
            .Returns("refresh_token");

        _jwtTokenServiceMock
            .Setup(x => x.HashRefreshToken(It.IsAny<string>()))
            .Returns("refresh_token_hash");

        _jwtTokenServiceMock
            .Setup(x => x.GetAccessTokenExpirationUtc())
            .Returns(DateTime.UtcNow.AddMinutes(15));

        _jwtTokenServiceMock
            .Setup(x => x.GetRefreshTokenExpirationUtc())
            .Returns(DateTime.UtcNow.AddDays(30));

        // Act
        var response = await _handler.HandleAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();
        response.Data!.Email.Should().Be("test@harmonie.chat");
        response.Data.Username.Should().Be("testuser");
        response.Data.AccessToken.Should().Be("access_token");
        response.Data.RefreshToken.Should().Be("refresh_token");

        _unitOfWorkMock.Verify(
            x => x.BeginAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        _userRepositoryMock.Verify(
            x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _refreshTokenRepositoryMock.Verify(
            x => x.StoreAsync(
                It.IsAny<UserId>(),
                "refresh_token_hash",
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _transactionMock.Verify(
            x => x.CommitAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        _transactionMock.Verify(
            x => x.DisposeAsync(),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenRefreshTokenStoreFails_ShouldDisposeWithoutCommit()
    {
        // Arrange
        var request = new RegisterRequest(
            "test@harmonie.chat",
            "testuser",
            "Test123!@#");

        _userRepositoryMock
            .Setup(x => x.CheckDuplicatesAsync(It.IsAny<Email>(), It.IsAny<Username>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserDuplicateCheck(false, false));

        _passwordHasherMock
            .Setup(x => x.HashPassword(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("hashed_password");

        _jwtTokenServiceMock
            .Setup(x => x.GenerateAccessToken(It.IsAny<UserId>(), It.IsAny<Email>(), It.IsAny<Username>()))
            .Returns("access_token");

        _jwtTokenServiceMock
            .Setup(x => x.GenerateRefreshToken())
            .Returns("refresh_token");

        _jwtTokenServiceMock
            .Setup(x => x.HashRefreshToken(It.IsAny<string>()))
            .Returns("refresh_token_hash");

        _jwtTokenServiceMock
            .Setup(x => x.GetAccessTokenExpirationUtc())
            .Returns(DateTime.UtcNow.AddMinutes(15));

        _jwtTokenServiceMock
            .Setup(x => x.GetRefreshTokenExpirationUtc())
            .Returns(DateTime.UtcNow.AddDays(30));

        _refreshTokenRepositoryMock
            .Setup(x => x.StoreAsync(
                It.IsAny<UserId>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Simulated write failure"));

        // Act
        var act = async () => await _handler.HandleAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();

        _unitOfWorkMock.Verify(
            x => x.BeginAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        _userRepositoryMock.Verify(
            x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _refreshTokenRepositoryMock.Verify(
            x => x.StoreAsync(
                It.IsAny<UserId>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _transactionMock.Verify(
            x => x.CommitAsync(It.IsAny<CancellationToken>()),
            Times.Never);

        _transactionMock.Verify(
            x => x.DisposeAsync(),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithDuplicateEmail_ShouldReturnFailure()
    {
        // Arrange
        var request = new RegisterRequest(
            "test@harmonie.chat",
            "testuser",
            "Test123!@#");

        _userRepositoryMock
            .Setup(x => x.CheckDuplicatesAsync(It.IsAny<Email>(), It.IsAny<Username>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserDuplicateCheck(true, false));

        // Act
        var response = await _handler.HandleAsync(request);

        // Assert
        response.Success.Should().BeFalse();
        response.Data.Should().BeNull();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Auth.DuplicateEmail);
    }

    [Fact]
    public async Task HandleAsync_WithDuplicateUsername_ShouldReturnFailure()
    {
        // Arrange
        var request = new RegisterRequest(
            "test@harmonie.chat",
            "testuser",
            "Test123!@#");

        _userRepositoryMock
            .Setup(x => x.CheckDuplicatesAsync(It.IsAny<Email>(), It.IsAny<Username>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserDuplicateCheck(false, true));

        // Act
        var response = await _handler.HandleAsync(request);

        // Assert
        response.Success.Should().BeFalse();
        response.Data.Should().BeNull();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Auth.DuplicateUsername);
    }

    [Fact]
    public async Task HandleAsync_WithoutAvatarAndTheme_ShouldReturnNullAvatarAndDefaultTheme()
    {
        // Arrange
        var request = new RegisterRequest(
            "test@harmonie.chat",
            "testuser",
            "Test123!@#");

        SetupSuccessfulTokenMocks();

        _userRepositoryMock
            .Setup(x => x.CheckDuplicatesAsync(It.IsAny<Email>(), It.IsAny<Username>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserDuplicateCheck(false, false));

        // Act
        var response = await _handler.HandleAsync(request);

        // Assert
        response.Success.Should().BeTrue();
        response.Data!.Avatar.Should().BeNull();
        response.Data.Theme.Should().Be("default");
    }

    [Fact]
    public async Task HandleAsync_WithFullAvatarAndTheme_ShouldReturnAvatarAndTheme()
    {
        // Arrange
        var request = new RegisterRequest(
            "test@harmonie.chat",
            "testuser",
            "Test123!@#",
            Avatar: new AvatarAppearanceDto("#ff0000", "star", "#0000ff"),
            Theme: "dark");

        SetupSuccessfulTokenMocks();

        _userRepositoryMock
            .Setup(x => x.CheckDuplicatesAsync(It.IsAny<Email>(), It.IsAny<Username>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserDuplicateCheck(false, false));

        // Act
        var response = await _handler.HandleAsync(request);

        // Assert
        response.Success.Should().BeTrue();
        response.Data!.Avatar.Should().NotBeNull();
        response.Data.Avatar!.Color.Should().Be("#ff0000");
        response.Data.Avatar.Icon.Should().Be("star");
        response.Data.Avatar.Bg.Should().Be("#0000ff");
        response.Data.Theme.Should().Be("dark");
    }

    [Fact]
    public async Task HandleAsync_WithPartialAvatar_ShouldReturnPartialAvatarAndDefaultTheme()
    {
        // Arrange
        var request = new RegisterRequest(
            "test@harmonie.chat",
            "testuser",
            "Test123!@#",
            Avatar: new AvatarAppearanceDto("#ff0000", null, null));

        SetupSuccessfulTokenMocks();

        _userRepositoryMock
            .Setup(x => x.CheckDuplicatesAsync(It.IsAny<Email>(), It.IsAny<Username>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserDuplicateCheck(false, false));

        // Act
        var response = await _handler.HandleAsync(request);

        // Assert
        response.Success.Should().BeTrue();
        response.Data!.Avatar.Should().NotBeNull();
        response.Data.Avatar!.Color.Should().Be("#ff0000");
        response.Data.Avatar.Icon.Should().BeNull();
        response.Data.Avatar.Bg.Should().BeNull();
        response.Data.Theme.Should().Be("default");
    }

    private void SetupSuccessfulTokenMocks()
    {
        _passwordHasherMock
            .Setup(x => x.HashPassword(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("hashed_password");

        _jwtTokenServiceMock
            .Setup(x => x.GenerateAccessToken(It.IsAny<UserId>(), It.IsAny<Email>(), It.IsAny<Username>()))
            .Returns("access_token");

        _jwtTokenServiceMock
            .Setup(x => x.GenerateRefreshToken())
            .Returns("refresh_token");

        _jwtTokenServiceMock
            .Setup(x => x.HashRefreshToken(It.IsAny<string>()))
            .Returns("refresh_token_hash");

        _jwtTokenServiceMock
            .Setup(x => x.GetAccessTokenExpirationUtc())
            .Returns(DateTime.UtcNow.AddMinutes(15));

        _jwtTokenServiceMock
            .Setup(x => x.GetRefreshTokenExpirationUtc())
            .Returns(DateTime.UtcNow.AddDays(30));
    }
}
