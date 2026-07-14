using FluentAssertions;
using Harmonie.Infrastructure.Services.Notifications;
using Xunit;

namespace Harmonie.Infrastructure.Tests.Notifications;

public sealed class WebPushEndpointValidatorTests
{
    private readonly WebPushEndpointValidator _validator = new();

    [Theory]
    [InlineData("http://updates.push.services.mozilla.com/subscription")]
    [InlineData("https://localhost/subscription")]
    [InlineData("https://127.0.0.1/subscription")]
    [InlineData("https://10.0.0.1/subscription")]
    [InlineData("https://192.168.1.10/subscription")]
    [InlineData("https://169.254.1.1/subscription")]
    public async Task IsAllowedAsync_WithUnsafeEndpoint_ShouldReturnFalse(string endpoint)
    {
        var allowed = await _validator.IsAllowedAsync(endpoint, TestContext.Current.CancellationToken);

        allowed.Should().BeFalse();
    }

    [Fact]
    public async Task IsAllowedAsync_WithPublicHttpsEndpoint_ShouldReturnTrue()
    {
        var allowed = await _validator.IsAllowedAsync(
            "https://1.1.1.1/subscription",
            TestContext.Current.CancellationToken);

        allowed.Should().BeTrue();
    }
}
