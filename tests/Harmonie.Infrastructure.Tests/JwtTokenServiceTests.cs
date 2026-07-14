using FluentAssertions;
using Harmonie.Domain.ValueObjects.Users;
using Harmonie.Infrastructure.Authentication;
using Harmonie.Infrastructure.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Harmonie.Infrastructure.Tests;

public sealed class JwtTokenServiceTests
{
    [Fact]
    public void ExpirationMethods_ShouldUseInjectedTimeProvider()
    {
        var timeProvider = TestTime.CreateProvider();
        var service = CreateService(timeProvider);

        service.GetAccessTokenExpirationUtc().Should().Be(TestTime.UtcNow.AddMinutes(15));
        service.GetRefreshTokenExpirationUtc().Should().Be(TestTime.UtcNow.AddDays(30));
    }

    [Fact]
    public void ValidateAccessToken_ShouldUseInjectedTimeProviderForLifetime()
    {
        var timeProvider = TestTime.CreateProvider();
        var service = CreateService(timeProvider);
        var userId = UserId.New();
        var email = Email.Create("clock@harmonie.chat").Value!;
        var username = Username.Create("clockuser").Value!;
        var token = service.GenerateAccessToken(userId, email, username);

        service.ValidateAccessToken(token, out var validatedUserId).Should().BeTrue();
        validatedUserId.Should().Be(userId);

        timeProvider.Advance(TimeSpan.FromMinutes(16));

        service.ValidateAccessToken(token, out validatedUserId).Should().BeFalse();
        validatedUserId.Should().BeNull();
    }

    private static JwtTokenService CreateService(TimeProvider timeProvider)
    {
        var settings = new JwtSettings
        {
            Secret = "test-secret-that-is-at-least-thirty-two-characters",
            Issuer = "harmonie-tests",
            Audience = "harmonie-tests",
            AccessTokenExpirationMinutes = 15,
            RefreshTokenExpirationDays = 30
        };

        return new JwtTokenService(
            Options.Create(settings),
            timeProvider,
            NullLogger<JwtTokenService>.Instance);
    }

}
