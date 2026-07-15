using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Application.Common.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Auth.Login;

/// <summary>
/// Endpoint for user login
/// </summary>
public static class LoginEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/login", HandleAsync)
            .WithName("Login")
            .WithTags("Auth")
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Auth.UserInactive);
    }

    private static async Task<IResult> HandleAsync(
        [FromBody] LoginRequest request,
        [FromServices] IHandler<LoginRequest, AuthSessionResult<LoginResponse>> handler,
        [FromServices] IValidator<LoginRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<LoginResponse>.Fail(validationError).ToHttpResult(httpContext);

        var result = await handler.HandleAsync(request, cancellationToken);
        if (!result.Success)
            return ApplicationResponse<LoginResponse>.Fail(result.Error).ToHttpResult(httpContext);

        RefreshTokenCookie.Write(
            httpContext,
            result.Data.RefreshToken,
            result.Data.RefreshTokenExpiresAt);

        return ApplicationResponse<LoginResponse>.Ok(result.Data.Response).ToHttpResult(httpContext);
    }
}
