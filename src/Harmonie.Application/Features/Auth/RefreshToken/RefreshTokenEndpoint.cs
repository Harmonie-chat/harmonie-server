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
            .Produces<ApplicationError>(StatusCodes.Status400BadRequest)
            .Produces<ApplicationError>(StatusCodes.Status401Unauthorized)
            .Produces<ApplicationError>(StatusCodes.Status403Forbidden)
            .Produces<ApplicationError>(StatusCodes.Status500InternalServerError);
    }

    private static async Task<IResult> HandleAsync(
        [FromBody] RefreshTokenRequest request,
        [FromServices] RefreshTokenHandler handler,
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
