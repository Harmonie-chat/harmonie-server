using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Application.Common.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Auth.RefreshToken;

/// <summary>
/// Endpoint for refreshing JWT access tokens.
/// </summary>
public static class RefreshTokenEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/refresh", HandleAsync)
            .WithName("RefreshToken")
            .WithTags("Auth")
            .WithSummary("Refresh access token")
            .WithDescription("Rotates refresh token and returns a new access token without requiring a new login.")
            .Produces<RefreshTokenResponse>(StatusCodes.Status200OK)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidRefreshToken,
                ApplicationErrorCodes.Auth.RefreshTokenReuseDetected,
                ApplicationErrorCodes.Auth.UserInactive);
    }

    private static async Task<IResult> HandleAsync(
        [FromBody] RefreshTokenRequest request,
        [FromServices] IHandler<RefreshTokenRequest, RefreshTokenResponse> handler,
        [FromServices] IValidator<RefreshTokenRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var cookieRefreshToken = RefreshTokenCookie.Read(httpContext);
        var effectiveRequest = string.IsNullOrWhiteSpace(request.RefreshToken)
            && !string.IsNullOrWhiteSpace(cookieRefreshToken)
                ? new RefreshTokenRequest(cookieRefreshToken)
                : request;

        var validationError = await effectiveRequest.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
        {
            RefreshTokenCookie.Delete(httpContext);
            return ApplicationResponse<RefreshTokenResponse>.Fail(validationError).ToHttpResult(httpContext);
        }

        var response = await handler.HandleAsync(effectiveRequest, cancellationToken);
        if (response.Success)
        {
            RefreshTokenCookie.Write(
                httpContext,
                response.Data.RefreshToken,
                response.Data.RefreshTokenExpiresAt);
        }
        else
        {
            RefreshTokenCookie.Delete(httpContext);
        }

        return response.ToHttpResult(httpContext);
    }
}
