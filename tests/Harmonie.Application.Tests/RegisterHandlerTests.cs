using FluentAssertions;
using Harmonie.Application.Features.Auth.Register;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.Exceptions;
using Harmonie.Domain.ValueObjects;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests;

/// <summary>
/// Tests for RegisterHandler
/// </summary>
public sealed class RegisterHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IPasswordHasher> _passwordHasherMock;
    private readonly Mock<IJwtTokenService> _jwtTokenServiceMock;
    private readonly RegisterHandler _handler;

    public RegisterHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _passwordHasherMock = new Mock<IPasswordHasher>();
        _jwtTokenServiceMock = new Mock<IJwtTokenService>();
        
        _handler = new RegisterHandler(
            _userRepositoryMock.Object,
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
            .Setup(x => x.ExistsByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _userRepositoryMock
            .Setup(x => x.ExistsByUsernameAsync(It.IsAny<Username>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _passwordHasherMock
            .Setup(x => x.HashPassword(It.IsAny<string>(),It.IsAny<string>()))
            .Returns("hashed_password");

        _jwtTokenServiceMock
            .Setup(x => x.GenerateAccessToken(It.IsAny<UserId>(), It.IsAny<Email>(), It.IsAny<Username>()))
            .Returns("access_token");

        _jwtTokenServiceMock
            .Setup(x => x.GenerateRefreshToken())
            .Returns("refresh_token");

        // Act
        var response = await _handler.HandleAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Email.Should().Be("test@harmonie.chat");
        response.Username.Should().Be("testuser");
        response.AccessToken.Should().Be("access_token");
        response.RefreshToken.Should().Be("refresh_token");

        _userRepositoryMock.Verify(
            x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithDuplicateEmail_ShouldThrow()
    {
        // Arrange
        var request = new RegisterRequest(
            "test@harmonie.chat",
            "testuser",
            "Test123!@#");

        _userRepositoryMock
            .Setup(x => x.ExistsByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act & Assert
        await Assert.ThrowsAsync<DuplicateEmailException>(
            () => _handler.HandleAsync(request));
    }

    [Fact]
    public async Task HandleAsync_WithDuplicateUsername_ShouldThrow()
    {
        // Arrange
        var request = new RegisterRequest(
            "test@harmonie.chat",
            "testuser",
            "Test123!@#");

        _userRepositoryMock
            .Setup(x => x.ExistsByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _userRepositoryMock
            .Setup(x => x.ExistsByUsernameAsync(It.IsAny<Username>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act & Assert
        await Assert.ThrowsAsync<DuplicateUsernameException>(
            () => _handler.HandleAsync(request));
    }
}
