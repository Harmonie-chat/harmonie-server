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
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Auth.UserInactive);
    }

    private static async Task<IResult> HandleAsync(
        [FromBody] LoginRequest request,
        [FromServices] IHandler<LoginRequest, LoginResponse> handler,
        [FromServices] IValidator<LoginRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<LoginResponse>.Fail(validationError).ToHttpResult(httpContext);

        var response = await handler.HandleAsync(request, cancellationToken);

        return response.ToHttpResult(httpContext);
    }
}
