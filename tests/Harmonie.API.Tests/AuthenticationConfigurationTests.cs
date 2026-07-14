using FluentAssertions;
using Harmonie.API.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Harmonie.API.Tests;

public sealed class AuthenticationConfigurationTests
{
    [Fact]
    public void JwtLifetimeValidator_ShouldUseRegisteredTimeProvider()
    {
        var timeProvider = TestTime.CreateProvider();
        var nowUtc = timeProvider.GetUtcNow();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "test-secret-that-is-at-least-thirty-two-characters",
                ["Jwt:Issuer"] = "harmonie-tests",
                ["Jwt:Audience"] = "harmonie-tests"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(timeProvider);
        services.AddJwtAuthentication(configuration);
        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        var lifetimeValidator = options.TokenValidationParameters.LifetimeValidator;

        lifetimeValidator.Should().NotBeNull();
        lifetimeValidator!(
            nowUtc.UtcDateTime.AddMinutes(-1),
            nowUtc.UtcDateTime.AddMinutes(1),
            null!,
            options.TokenValidationParameters).Should().BeTrue();
        lifetimeValidator(
            nowUtc.UtcDateTime.AddMinutes(-2),
            nowUtc.UtcDateTime.AddMinutes(-1),
            null!,
            options.TokenValidationParameters).Should().BeFalse();
    }

}
