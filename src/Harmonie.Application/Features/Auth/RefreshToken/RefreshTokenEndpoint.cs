using FluentValidation;
using Harmonie.Application.Common;
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
        CancellationToken cancellationToken)
    {
        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<RefreshTokenResponse>.Fail(validationError).ToHttpResult();

        var response = await handler.HandleAsync(request, cancellationToken);
        return response.ToHttpResult();
    }
}
