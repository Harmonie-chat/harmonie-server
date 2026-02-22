using FluentAssertions;
using Harmonie.Application.Features.Auth.Login;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.Exceptions;
using Harmonie.Domain.ValueObjects;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests;

/// <summary>
/// Tests for LoginHandler.
/// </summary>
public sealed class LoginHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly Mock<IPasswordHasher> _passwordHasherMock;
    private readonly Mock<IJwtTokenService> _jwtTokenServiceMock;
    private readonly LoginHandler _handler;

    public LoginHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _refreshTokenRepositoryMock = new Mock<IRefreshTokenRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = new Mock<IUnitOfWorkTransaction>();
        _passwordHasherMock = new Mock<IPasswordHasher>();
        _jwtTokenServiceMock = new Mock<IJwtTokenService>();

        _transactionMock
            .Setup(x => x.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        _unitOfWorkMock
            .Setup(x => x.BeginAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_transactionMock.Object);

        _handler = new LoginHandler(
            _userRepositoryMock.Object,
            _refreshTokenRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _passwordHasherMock.Object,
            _jwtTokenServiceMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WithValidEmail_ShouldSucceedAndCommitTransaction()
    {
        // Arrange
        var user = CreateActiveUser();
        var request = new LoginRequest("test@harmonie.chat", "Test123!@#");

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _passwordHasherMock
            .Setup(x => x.VerifyPassword(It.IsAny<string>(), user.PasswordHash, request.Password))
            .Returns(true);

        _jwtTokenServiceMock
            .Setup(x => x.GenerateAccessToken(user.Id, user.Email, user.Username))
            .Returns("access_token");

        _jwtTokenServiceMock
            .Setup(x => x.GenerateRefreshToken())
            .Returns("refresh_token");

        _jwtTokenServiceMock
            .Setup(x => x.HashRefreshToken("refresh_token"))
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
        response.Email.Should().Be(user.Email.Value);
        response.Username.Should().Be(user.Username.Value);
        response.AccessToken.Should().Be("access_token");
        response.RefreshToken.Should().Be("refresh_token");

        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Once);
        _userRepositoryMock.Verify(x => x.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _refreshTokenRepositoryMock.Verify(
            x => x.StoreAsync(user.Id, "refresh_token_hash", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _transactionMock.Verify(x => x.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithValidUsername_ShouldResolveByUsername()
    {
        // Arrange
        var user = CreateActiveUser();
        var request = new LoginRequest("testuser", "Test123!@#");

        _userRepositoryMock
            .Setup(x => x.GetByUsernameAsync(It.IsAny<Username>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _passwordHasherMock
            .Setup(x => x.VerifyPassword(It.IsAny<string>(), user.PasswordHash, request.Password))
            .Returns(true);

        _jwtTokenServiceMock
            .Setup(x => x.GenerateAccessToken(user.Id, user.Email, user.Username))
            .Returns("access_token");

        _jwtTokenServiceMock
            .Setup(x => x.GenerateRefreshToken())
            .Returns("refresh_token");

        _jwtTokenServiceMock
            .Setup(x => x.HashRefreshToken("refresh_token"))
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
        response.Username.Should().Be("testuser");
        _userRepositoryMock.Verify(x => x.GetByUsernameAsync(It.IsAny<Username>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithUnknownIdentity_ShouldThrowInvalidPasswordException()
    {
        // Arrange
        var request = new LoginRequest("missing@harmonie.chat", "Test123!@#");

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidPasswordException>(() => _handler.HandleAsync(request));

        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithInactiveUser_ShouldThrowUserInactiveException()
    {
        // Arrange
        var user = CreateInactiveUser();
        var request = new LoginRequest("test@harmonie.chat", "Test123!@#");

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act & Assert
        await Assert.ThrowsAsync<UserInactiveException>(() => _handler.HandleAsync(request));

        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithInvalidPassword_ShouldThrowInvalidPasswordException()
    {
        // Arrange
        var user = CreateActiveUser();
        var request = new LoginRequest("test@harmonie.chat", "WrongPassword123");

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _passwordHasherMock
            .Setup(x => x.VerifyPassword(It.IsAny<string>(), user.PasswordHash, request.Password))
            .Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidPasswordException>(() => _handler.HandleAsync(request));

        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenRefreshTokenStoreFails_ShouldDisposeWithoutCommit()
    {
        // Arrange
        var user = CreateActiveUser();
        var request = new LoginRequest("test@harmonie.chat", "Test123!@#");

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _passwordHasherMock
            .Setup(x => x.VerifyPassword(It.IsAny<string>(), user.PasswordHash, request.Password))
            .Returns(true);

        _jwtTokenServiceMock
            .Setup(x => x.GenerateAccessToken(user.Id, user.Email, user.Username))
            .Returns("access_token");

        _jwtTokenServiceMock
            .Setup(x => x.GenerateRefreshToken())
            .Returns("refresh_token");

        _jwtTokenServiceMock
            .Setup(x => x.HashRefreshToken("refresh_token"))
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

        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Once);
        _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        _transactionMock.Verify(x => x.DisposeAsync(), Times.Once);
    }

    private static User CreateActiveUser()
    {
        var emailResult = Email.Create("test@harmonie.chat");
        var usernameResult = Username.Create("testuser");

        if (emailResult.IsFailure || emailResult.Value is null)
            throw new InvalidOperationException("Failed to create valid email for test.");

        if (usernameResult.IsFailure || usernameResult.Value is null)
            throw new InvalidOperationException("Failed to create valid username for test.");

        var userResult = User.Create(
            emailResult.Value,
            usernameResult.Value,
            "hashed_password");

        if (userResult.IsFailure || userResult.Value is null)
            throw new InvalidOperationException("Failed to create valid user for test.");

        return userResult.Value;
    }

    private static User CreateInactiveUser()
    {
        var user = CreateActiveUser();
        var deactivateResult = user.Deactivate();
        if (deactivateResult.IsFailure)
            throw new InvalidOperationException(deactivateResult.Error ?? "Failed to deactivate test user.");

        return user;
    }
}
