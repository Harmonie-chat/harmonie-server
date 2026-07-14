using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Notifications.RegisterWebPushDevice;
using Harmonie.Application.Interfaces.Notifications;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Application.Tests.Common;
using Harmonie.Domain.Entities.Users;
using Harmonie.Domain.ValueObjects.Users;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Notifications;

public sealed class RegisterWebPushDeviceTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<INotificationDeviceRepository> _notificationDeviceRepositoryMock = new();

    [Fact]
    public async Task Handler_WithValidWebPushSubscription_ShouldUpsertWebPushDevice()
    {
        var user = ApplicationTestBuilders.CreateUser();
        var expirationTime = new DateTimeOffset(TestClock.UtcNow).AddDays(30).ToUnixTimeMilliseconds();
        var request = CreateRequest(expirationTime);
        var handler = CreateHandler();

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var response = await handler.HandleAsync(request, user.Id, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data.Should().BeTrue();

        _notificationDeviceRepositoryMock.Verify(
            x => x.UpsertWebPushAsync(
                It.Is<WebPushNotificationDeviceRegistration>(registration =>
                    registration.UserId == user.Id &&
                    registration.Endpoint == request.Endpoint &&
                    registration.P256dh == request.Keys.P256dh &&
                    registration.Auth == request.Keys.Auth &&
                    registration.ExpiresAtUtc.HasValue),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handler_WhenUserDoesNotExist_ShouldReturnNotFoundAndNotUpsert()
    {
        var userId = UserId.New();
        var handler = CreateHandler();

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var response = await handler.HandleAsync(CreateRequest(), userId, TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.User.NotFound);

        _notificationDeviceRepositoryMock.Verify(
            x => x.UpsertWebPushAsync(
                It.IsAny<WebPushNotificationDeviceRegistration>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(null, "p256dh", "auth")]
    [InlineData("", "p256dh", "auth")]
    [InlineData("http://push.example/subscription", "p256dh", "auth")]
    [InlineData("not-a-uri", "p256dh", "auth")]
    [InlineData("https://push.example/subscription", "", "auth")]
    [InlineData("https://push.example/subscription", "p256dh", "")]
    public async Task Validator_WithInvalidRequest_ShouldFail(
        string? endpoint,
        string p256dh,
        string auth)
    {
        var validator = new RegisterWebPushDeviceValidator();
        var request = new RegisterWebPushDeviceRequest(
            endpoint ?? string.Empty,
            ExpirationTime: null,
            new RegisterWebPushDeviceKeysRequest(p256dh, auth));

        var result = await validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validator_WithValidRequest_ShouldPass()
    {
        var validator = new RegisterWebPushDeviceValidator();

        var result = await validator.ValidateAsync(CreateRequest(), TestContext.Current.CancellationToken);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public async Task Validator_WithInvalidExpirationTime_ShouldFail(long expirationTime)
    {
        var validator = new RegisterWebPushDeviceValidator();
        var request = CreateRequest(expirationTime);

        var result = await validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        result.IsValid.Should().BeFalse();
    }

    private RegisterWebPushDeviceHandler CreateHandler()
        => new(
            _userRepositoryMock.Object,
            _notificationDeviceRepositoryMock.Object);

    private static RegisterWebPushDeviceRequest CreateRequest(long? expirationTime = null)
        => new(
            "https://push.example/subscription/123",
            expirationTime,
            new RegisterWebPushDeviceKeysRequest(
                "p256dh-key",
                "auth-secret"));
}
