using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Common.Auth;
using Harmonie.Application.Features.Auth.LogoutAll;
using Harmonie.Application.Interfaces.Auth;
using Harmonie.Domain.ValueObjects.Users;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Auth;

/// <summary>
/// Tests for LogoutAllHandler.
/// </summary>
public sealed class LogoutAllHandlerTests
{
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepositoryMock;
    private readonly LogoutAllHandler _handler;

    public LogoutAllHandlerTests()
    {
        _refreshTokenRepositoryMock = new Mock<IRefreshTokenRepository>();
        _handler = new LogoutAllHandler(
            _refreshTokenRepositoryMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WithAuthenticatedUser_ShouldRevokeAllActiveSessionsAndSucceed()
    {
        // Arrange
        var currentUserId = UserId.New();

        _refreshTokenRepositoryMock
            .Setup(x => x.RevokeAllActiveAsync(
                currentUserId,
                It.IsAny<DateTime>(),
                RefreshTokenRevocationReasons.LogoutAll,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var response = await _handler.HandleAsync(Unit.Value, currentUserId);

        // Assert
        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();

        _refreshTokenRepositoryMock.Verify(
            x => x.RevokeAllActiveAsync(
                currentUserId,
                It.IsAny<DateTime>(),
                RefreshTokenRevocationReasons.LogoutAll,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
