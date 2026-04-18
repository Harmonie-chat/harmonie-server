using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Uploads;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Conversations.DeleteMessageAttachment;

public static class DeleteMessageAttachmentEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/conversations/{conversationId}/messages/{messageId}/attachments/{attachmentId}", HandleAsync)
            .WithName("DeleteConversationMessageAttachment")
            .WithTags("Conversations")
            .RequireAuthorization()
            .WithSummary("Delete a conversation message attachment")
            .WithDescription("Deletes a specific attachment from a conversation message. Only the message author can delete attachments from their own messages.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Conversation.NotFound,
                ApplicationErrorCodes.Conversation.AccessDenied,
                ApplicationErrorCodes.Message.NotFound,
                ApplicationErrorCodes.Message.AttachmentNotFound,
                ApplicationErrorCodes.Message.DeleteForbidden);
    }

    private static async Task<IResult> HandleAsync(
        ConversationId conversationId,
        MessageId messageId,
        UploadedFileId attachmentId,
        [FromServices] IAuthenticatedHandler<DeleteConversationMessageAttachmentInput, bool> handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var callerId = httpContext.GetRequiredAuthenticatedUserId();
        var response = await handler.HandleAsync(
            new DeleteConversationMessageAttachmentInput(conversationId, messageId, attachmentId),
            callerId,
            cancellationToken);

        if (response.Success)
            return Results.NoContent();

        return response.ToHttpResult(httpContext);
    }
}
