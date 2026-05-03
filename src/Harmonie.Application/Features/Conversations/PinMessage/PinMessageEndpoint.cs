using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Conversations.PinMessage;

public static class PinMessageEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/conversations/{conversationId}/messages/{messageId}/pin", HandleAsync)
            .WithName("PinConversationMessage")
            .WithTags("Conversations")
            .RequireAuthorization()
            .WithSummary("Pin a message in a conversation")
            .WithDescription("Pins a message in a conversation. Idempotent — pinning an already-pinned message is a no-op.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Conversation.NotFound,
                ApplicationErrorCodes.Conversation.AccessDenied,
                ApplicationErrorCodes.Pin.MessageNotFound);
    }

    private static async Task<IResult> HandleAsync(
        ConversationId conversationId,
        MessageId messageId,
        [FromServices] IAuthenticatedHandler<ConversationPinMessageInput, bool> handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var callerId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(
            new ConversationPinMessageInput(conversationId, messageId),
            callerId,
            cancellationToken);

        if (response.Success)
            return Results.NoContent();

        return response.ToHttpResult(httpContext);
    }
}
