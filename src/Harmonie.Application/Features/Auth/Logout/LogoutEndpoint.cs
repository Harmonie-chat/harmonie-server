using FluentValidation;
using Harmonie.Application.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Auth.Logout;

/// <summary>
/// Endpoint for logging out current authenticated session.
/// </summary>
public static class LogoutEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/logout", HandleAsync)
            .WithName("Logout")
            .WithTags("Auth")
            .RequireAuthorization()
            .WithSummary("Logout current session")
            .WithDescription("Revokes the provided refresh token for the authenticated user.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ApplicationError>(StatusCodes.Status400BadRequest)
            .Produces<ApplicationError>(StatusCodes.Status401Unauthorized)
            .Produces<ApplicationError>(StatusCodes.Status500InternalServerError);
    }

    private static async Task<IResult> HandleAsync(
        [FromBody] LogoutRequest request,
        [FromServices] LogoutHandler handler,
        [FromServices] IValidator<LogoutRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<LogoutResponse>.Fail(validationError).ToHttpResult();

        if (!httpContext.TryGetAuthenticatedUserId(out var currentUserId) || currentUserId is null)
        {
            return ApplicationResponse<LogoutResponse>.Fail(
                    ApplicationErrorCodes.Auth.InvalidCredentials,
                    "Authenticated user identifier is missing.")
                .ToHttpResult();
        }

        var response = await handler.HandleAsync(request, currentUserId, cancellationToken);
        if (!response.Success)
            return response.ToHttpResult();

        return Results.NoContent();
    }
}
