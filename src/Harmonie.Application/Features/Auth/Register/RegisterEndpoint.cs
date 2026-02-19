using FluentValidation;
using Harmonie.Application.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;


namespace Harmonie.Application.Features.Auth.Register;

/// <summary>
/// Endpoint for user registration
/// </summary>
public static class RegisterEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/register", HandleAsync)
            .WithName("Register")
            .WithTags("Auth")
            .WithSummary("Register a new user account")
            .WithDescription("Creates a new user with email, username, and password. Returns JWT tokens for authentication.")
            .Produces<RegisterResponse>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .ProducesValidationProblem();
    }

    private static async Task<IResult> HandleAsync(
        [FromBody] RegisterRequest request,
        [FromServices] RegisterHandler handler,
        [FromServices] IValidator<RegisterRequest> validator,
        CancellationToken cancellationToken)
    {
        // Validate request
        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError != null)
            return validationError;

        // Handle registration
        var response = await handler.HandleAsync(request, cancellationToken);

        // Return created response
        return Results.Created($"/api/users/{response.UserId}", response);
    }
}
