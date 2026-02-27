using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Auth.Logout;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.ValueObjects;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests;

/// <summary>
/// Tests for LogoutHandler.
/// </summary>
public sealed class LogoutHandlerTests
{
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepositoryMock;
    private readonly Mock<IJwtTokenService> _jwtTokenServiceMock;
    private readonly LogoutHandler _handler;

    public LogoutHandlerTests()
    {
        _refreshTokenRepositoryMock = new Mock<IRefreshTokenRepository>();
        _jwtTokenServiceMock = new Mock<IJwtTokenService>();

        _handler = new LogoutHandler(
            _refreshTokenRepositoryMock.Object,
            _jwtTokenServiceMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WithValidSessionToken_ShouldRevokeAndSucceed()
    {
        // Arrange
        var currentUserId = UserId.New();
        var request = new LogoutRequest("refresh_token");

        _jwtTokenServiceMock
            .Setup(x => x.HashRefreshToken("refresh_token"))
            .Returns("refresh_token_hash");

        _refreshTokenRepositoryMock
            .Setup(x => x.RevokeActiveAsync(
                currentUserId,
                "refresh_token_hash",
                It.IsAny<DateTime>(),
                RefreshTokenRevocationReasons.Logout,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var response = await _handler.HandleAsync(request, currentUserId);

        // Assert
        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();

        _refreshTokenRepositoryMock.Verify(
            x => x.RevokeActiveAsync(
                currentUserId,
                "refresh_token_hash",
                It.IsAny<DateTime>(),
                RefreshTokenRevocationReasons.Logout,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithInvalidRefreshToken_ShouldReturnInvalidRefreshTokenFailure()
    {
        // Arrange
        var currentUserId = UserId.New();
        var request = new LogoutRequest("invalid_token");

        _jwtTokenServiceMock
            .Setup(x => x.HashRefreshToken("invalid_token"))
            .Returns("invalid_hash");

        _refreshTokenRepositoryMock
            .Setup(x => x.RevokeActiveAsync(
                currentUserId,
                "invalid_hash",
                It.IsAny<DateTime>(),
                RefreshTokenRevocationReasons.Logout,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var response = await _handler.HandleAsync(request, currentUserId);

        // Assert
        response.Success.Should().BeFalse();
        response.Data.Should().BeNull();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Auth.InvalidRefreshToken);
    }
}
