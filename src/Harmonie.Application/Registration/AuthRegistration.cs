using Harmonie.Application.Common;
using Harmonie.Application.Features.Auth.Login;
using Harmonie.Application.Features.Auth.Logout;
using Harmonie.Application.Features.Auth.LogoutAll;
using Harmonie.Application.Features.Auth.RefreshToken;
using Harmonie.Application.Features.Auth.Register;
using Microsoft.Extensions.DependencyInjection;

namespace Harmonie.Application.Registration;

public static class AuthRegistration
{
    public static IServiceCollection AddAuthHandlers(this IServiceCollection services)
    {
        services.AddHandler<LoginRequest, LoginResponse, LoginHandler>();
        services.AddHandler<RegisterRequest, RegisterResponse, RegisterHandler>();
        services.AddHandler<RefreshTokenRequest, RefreshTokenResponse, RefreshTokenHandler>();
        services.AddAuthenticatedHandler<LogoutRequest, LogoutResponse, LogoutHandler>();
        services.AddAuthenticatedHandler<Unit, LogoutAllResponse, LogoutAllHandler>();

        return services;
    }
}
