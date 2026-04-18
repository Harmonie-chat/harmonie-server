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
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Common.DomainRuleViolation,
                ApplicationErrorCodes.Auth.DuplicateEmail,
                ApplicationErrorCodes.Auth.DuplicateUsername);
    }

    private static async Task<IResult> HandleAsync(
        [FromBody] RegisterRequest request,
        [FromServices] IHandler<RegisterRequest, RegisterResponse> handler,
        [FromServices] IValidator<RegisterRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<RegisterResponse>.Fail(validationError).ToHttpResult(httpContext);

        var response = await handler.HandleAsync(request, cancellationToken);

        return response.ToCreatedHttpResult(data => $"/api/users/{data.UserId}", httpContext);
    }
}
