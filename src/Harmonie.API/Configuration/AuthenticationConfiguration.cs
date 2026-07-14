using System.Text;
using Harmonie.Application.Common;
using Harmonie.Application.Common.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Harmonie.API.Configuration;

public static class AuthenticationConfiguration
{
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSecret = configuration["Jwt:Secret"];
        if (string.IsNullOrWhiteSpace(jwtSecret))
            throw new InvalidOperationException("Configuration value 'Jwt:Secret' is required.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrWhiteSpace(accessToken)
                            && path.StartsWithSegments("/hubs/realtime"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    },
                    OnChallenge = context =>
                    {
                        context.HandleResponse();

                        return EndpointExtensions.WriteErrorAsync(
                            context.Response,
                            new ApplicationError(
                                ApplicationErrorCodes.Auth.InvalidCredentials,
                                "Authentication is required to access this resource."));
                    }
                };

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                    ValidateIssuer = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = configuration["Jwt:Audience"],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
            });

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<TimeProvider>((options, timeProvider) =>
            {
                options.TokenValidationParameters.LifetimeValidator =
                    (notBefore, expires, _, _) => IsLifetimeValid(notBefore, expires, timeProvider);
            });

        services.AddAuthorization();

        return services;
    }

    private static bool IsLifetimeValid(
        DateTime? notBefore,
        DateTime? expires,
        TimeProvider timeProvider)
    {
        return TokenLifetimeValidator.IsValid(
            notBefore,
            expires,
            timeProvider.GetUtcNow().UtcDateTime);
    }
}
