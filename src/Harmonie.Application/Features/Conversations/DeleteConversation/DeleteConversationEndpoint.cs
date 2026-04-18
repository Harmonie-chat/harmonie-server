using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects.Conversations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Conversations.DeleteConversation;

public static class DeleteConversationEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/conversations/{conversationId}", HandleAsync)
            .WithName("DeleteConversation")
            .WithTags("Conversations")
            .RequireAuthorization()
            .WithSummary("Delete a conversation")
            .WithDescription("Removes the authenticated user from a conversation. If no participants remain, the conversation is permanently deleted.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesErrors(
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Conversation.NotFound,
                ApplicationErrorCodes.Conversation.AccessDenied);
    }

    private static async Task<IResult> HandleAsync(
        ConversationId conversationId,
        [FromServices] IAuthenticatedHandler<DeleteConversationInput, bool> handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var callerId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(new DeleteConversationInput(conversationId), callerId, cancellationToken);
        if (response.Success)
            return Results.NoContent();

        return response.ToHttpResult(httpContext);
    }
}
