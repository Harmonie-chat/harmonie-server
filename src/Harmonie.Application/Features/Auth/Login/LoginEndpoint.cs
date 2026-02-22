using FluentValidation;
using Harmonie.Application.Common;
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
            /*.WithOpenApi(operation =>
            {
                operation.Summary = "Login to user account";
                operation.Description = "Authenticates user with email/username and password. Returns JWT tokens.";
                return operation;
            })*/
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .Produces<ApplicationError>(StatusCodes.Status400BadRequest)
            .Produces<ApplicationError>(StatusCodes.Status401Unauthorized)
            .Produces<ApplicationError>(StatusCodes.Status403Forbidden)
            .Produces<ApplicationError>(StatusCodes.Status500InternalServerError);
    }

    private static async Task<IResult> HandleAsync(
        [FromBody] LoginRequest request,
        [FromServices] LoginHandler handler,
        [FromServices] IValidator<LoginRequest> validator,
        CancellationToken cancellationToken)
    {
        // Validate request
        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError != null)
            return ApplicationResponse<LoginResponse>.Fail(validationError).ToHttpResult();

        // Handle login
        var response = await handler.HandleAsync(request, cancellationToken);

        return response.ToHttpResult();
    }
}
