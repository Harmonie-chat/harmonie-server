using Harmonie.Application.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Conversations.ListConversations;

public static class ListConversationsEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/conversations", HandleAsync)
            .WithName("ListConversations")
            .WithTags("Conversations")
            .RequireAuthorization()
            .WithSummary("List current user conversations")
            .WithDescription("Returns direct conversations for the authenticated user with the other participant's basic profile info.")
            .Produces<ListConversationsResponse>(StatusCodes.Status200OK)
            .ProducesErrors(
                ApplicationErrorCodes.Auth.InvalidCredentials);
    }

    private static async Task<IResult> HandleAsync(
        [FromServices] IAuthenticatedHandler<Unit, ListConversationsResponse> handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var currentUserId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(Unit.Value, currentUserId, cancellationToken);
        return response.ToHttpResult();
    }
}
