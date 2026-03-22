using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Conversations.DeleteMessage;

public static class DeleteMessageEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/conversations/{conversationId}/messages/{messageId}", HandleAsync)
            .WithName("DeleteConversationMessage")
            .WithTags("Conversations")
            .RequireAuthorization()
            .WithSummary("Delete a conversation message")
            .WithDescription("Soft-deletes a conversation message. Only the message author can delete their own messages.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Conversation.NotFound,
                ApplicationErrorCodes.Conversation.AccessDenied,
                ApplicationErrorCodes.Message.NotFound,
                ApplicationErrorCodes.Message.DeleteForbidden);
    }

    private static async Task<IResult> HandleAsync(
        ConversationId conversationId,
        MessageId messageId,
        [FromServices] IAuthenticatedHandler<DeleteConversationMessageInput, bool> handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var callerId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(new DeleteConversationMessageInput(conversationId, messageId), callerId, cancellationToken);
        if (response.Success)
            return Results.NoContent();

        return response.ToHttpResult();
    }
}
