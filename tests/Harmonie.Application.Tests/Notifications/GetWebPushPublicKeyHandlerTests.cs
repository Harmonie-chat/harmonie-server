using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Notifications.GetWebPushPublicKey;
using Harmonie.Application.Interfaces.Notifications;
using Xunit;

namespace Harmonie.Application.Tests.Notifications;

public sealed class GetWebPushPublicKeyHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenPublicKeyIsConfigured_ShouldReturnPublicKey()
    {
        var handler = new GetWebPushPublicKeyHandler(new StubWebPushPublicKeyProvider("public-key"));

        var response = await handler.HandleAsync(Unit.Value, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data!.PublicKey.Should().Be("public-key");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_WhenPublicKeyIsMissing_ShouldReturnConfigurationFailure(string? publicKey)
    {
        var handler = new GetWebPushPublicKeyHandler(new StubWebPushPublicKeyProvider(publicKey));

        var response = await handler.HandleAsync(Unit.Value, TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Notification.WebPushNotConfigured);
    }

    private sealed class StubWebPushPublicKeyProvider : IWebPushPublicKeyProvider
    {
        private readonly string? _publicKey;

        public StubWebPushPublicKeyProvider(string? publicKey)
        {
            _publicKey = publicKey;
        }

        public string? GetPublicKey() => _publicKey;
    }
}
