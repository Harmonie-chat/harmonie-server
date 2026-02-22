using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Auth.RefreshToken;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.ValueObjects;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests;

/// <summary>
/// Tests for RefreshTokenHandler.
/// </summary>
public sealed class RefreshTokenHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepositoryMock;
    private readonly Mock<IJwtTokenService> _jwtTokenServiceMock;
    private readonly RefreshTokenHandler _handler;

    public RefreshTokenHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _refreshTokenRepositoryMock = new Mock<IRefreshTokenRepository>();
        _jwtTokenServiceMock = new Mock<IJwtTokenService>();

        _handler = new RefreshTokenHandler(
            _userRepositoryMock.Object,
            _refreshTokenRepositoryMock.Object,
            _jwtTokenServiceMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WithValidRefreshToken_ShouldRotateAndReturnNewTokens()
    {
        // Arrange
        var user = CreateValidUser();
        var session = new RefreshTokenSession(
            Id: Guid.NewGuid(),
            UserId: user.Id,
            ExpiresAtUtc: DateTime.UtcNow.AddMinutes(10),
            RevokedAtUtc: null);

        _jwtTokenServiceMock
            .Setup(x => x.HashRefreshToken(It.IsAny<string>()))
            .Returns((string token) => token == "old_refresh_token" ? "old_hash" : "new_hash");

        _jwtTokenServiceMock
            .Setup(x => x.GenerateAccessToken(user.Id, user.Email, user.Username))
            .Returns("new_access_token");

        _jwtTokenServiceMock
            .Setup(x => x.GenerateRefreshToken())
            .Returns("new_refresh_token");

        _jwtTokenServiceMock
            .Setup(x => x.GetAccessTokenExpirationUtc())
            .Returns(DateTime.UtcNow.AddMinutes(15));

        _jwtTokenServiceMock
            .Setup(x => x.GetRefreshTokenExpirationUtc())
            .Returns(DateTime.UtcNow.AddDays(30));

        _refreshTokenRepositoryMock
            .Setup(x => x.GetByTokenHashAsync("old_hash", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _refreshTokenRepositoryMock
            .Setup(x => x.RotateAsync(
                session.Id,
                user.Id,
                "new_hash",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new RefreshTokenRequest("old_refresh_token");

        // Act
        var response = await _handler.HandleAsync(request);

        // Assert
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Error.Should().BeNull();
        response.Data!.AccessToken.Should().Be("new_access_token");
        response.Data.RefreshToken.Should().Be("new_refresh_token");

        _refreshTokenRepositoryMock.Verify(x => x.RotateAsync(
                session.Id,
                user.Id,
                "new_hash",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithUnknownRefreshToken_ShouldReturnInvalidRefreshTokenFailure()
    {
        // Arrange
        _jwtTokenServiceMock
            .Setup(x => x.HashRefreshToken("unknown_refresh_token"))
            .Returns("unknown_hash");

        _refreshTokenRepositoryMock
            .Setup(x => x.GetByTokenHashAsync("unknown_hash", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshTokenSession?)null);

        var request = new RefreshTokenRequest("unknown_refresh_token");

        // Act
        var response = await _handler.HandleAsync(request);

        // Assert
        response.Success.Should().BeFalse();
        response.Data.Should().BeNull();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Auth.InvalidRefreshToken);
    }

    [Fact]
    public async Task HandleAsync_WithExpiredRefreshToken_ShouldReturnInvalidRefreshTokenFailure()
    {
        // Arrange
        var userId = UserId.New();
        var expiredSession = new RefreshTokenSession(
            Id: Guid.NewGuid(),
            UserId: userId,
            ExpiresAtUtc: DateTime.UtcNow.AddMinutes(-1),
            RevokedAtUtc: null);

        _jwtTokenServiceMock
            .Setup(x => x.HashRefreshToken("expired_refresh_token"))
            .Returns("expired_hash");

        _refreshTokenRepositoryMock
            .Setup(x => x.GetByTokenHashAsync("expired_hash", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expiredSession);

        var request = new RefreshTokenRequest("expired_refresh_token");

        // Act
        var response = await _handler.HandleAsync(request);

        // Assert
        response.Success.Should().BeFalse();
        response.Data.Should().BeNull();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Auth.InvalidRefreshToken);
    }

    private static User CreateValidUser()
    {
        var emailResult = Email.Create("test@harmonie.chat");
        var usernameResult = Username.Create("testuser");

        if (emailResult.IsFailure)
            throw new InvalidOperationException("Failed to create valid email for test.");

        if (usernameResult.IsFailure)
            throw new InvalidOperationException("Failed to create valid username for test.");

        var userResult = User.Create(
            emailResult.Value!,
            usernameResult.Value!,
            "hashed_password");

        if (userResult.IsFailure)
            throw new InvalidOperationException("Failed to create valid user for test.");

        return userResult.Value!;
    }
}
