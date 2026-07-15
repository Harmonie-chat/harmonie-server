using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Application.Common.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Auth.RefreshToken;

/// <summary>
/// Endpoint for refreshing an access token from the HttpOnly refresh cookie.
/// </summary>
public static class RefreshTokenEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/refresh", HandleAsync)
            .WithName("RefreshToken")
            .WithTags("Auth")
            .WithSummary("Refresh access token")
            .WithDescription("Rotates the HttpOnly refresh cookie and returns a new access token.")
            .Produces<RefreshTokenResponse>(StatusCodes.Status200OK)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidRefreshToken,
                ApplicationErrorCodes.Auth.RefreshTokenReuseDetected,
                ApplicationErrorCodes.Auth.UserInactive);
    }

    private static async Task<IResult> HandleAsync(
        [FromServices] IHandler<RefreshTokenRequest, AuthSessionResult<RefreshTokenResponse>> handler,
        [FromServices] IValidator<RefreshTokenRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var request = new RefreshTokenRequest(RefreshTokenCookie.Read(httpContext) ?? string.Empty);
        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
        {
            RefreshTokenCookie.Delete(httpContext);
            return ApplicationResponse<RefreshTokenResponse>.Fail(validationError).ToHttpResult(httpContext);
        }

        var result = await handler.HandleAsync(request, cancellationToken);
        if (!result.Success)
        {
            RefreshTokenCookie.Delete(httpContext);
            return ApplicationResponse<RefreshTokenResponse>.Fail(result.Error).ToHttpResult(httpContext);
        }

        RefreshTokenCookie.Write(
            httpContext,
            result.Data.RefreshToken,
            result.Data.RefreshTokenExpiresAt);

        return ApplicationResponse<RefreshTokenResponse>.Ok(result.Data.Response).ToHttpResult(httpContext);
    }
}
