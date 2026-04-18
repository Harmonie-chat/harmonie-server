using FluentValidation;
using Harmonie.Application.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Users.SearchUsers;

public static class SearchUsersEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/users/search", HandleAsync)
            .WithName("SearchUsers")
            .WithTags("Users")
            .RequireAuthorization()
            .WithSummary("Search users")
            .WithDescription("Searches users by username or display name, optionally scoped to a guild.")
            .Produces<SearchUsersResponse>(StatusCodes.Status200OK)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Guild.NotFound,
                ApplicationErrorCodes.Guild.AccessDenied);
    }

    private static async Task<IResult> HandleAsync(
        [AsParameters] SearchUsersRequest request,
        [FromServices] IAuthenticatedHandler<SearchUsersRequest, SearchUsersResponse> handler,
        [FromServices] IValidator<SearchUsersRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<SearchUsersResponse>.Fail(validationError).ToHttpResult(httpContext);

        var currentUserId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(request, currentUserId, cancellationToken);
        return response.ToHttpResult(httpContext);
    }
}
