using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Uploads;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Channels.DeleteMessageAttachment;

public static class DeleteMessageAttachmentEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/channels/{channelId}/messages/{messageId}/attachments/{attachmentId}", HandleAsync)
            .WithName("DeleteMessageAttachment")
            .WithTags("Channels")
            .RequireAuthorization()
            .WithSummary("Delete a message attachment")
            .WithDescription("Deletes a specific attachment from a message. Only the message author can delete attachments from their own messages.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Channel.NotFound,
                ApplicationErrorCodes.Channel.NotText,
                ApplicationErrorCodes.Channel.AccessDenied,
                ApplicationErrorCodes.Message.NotFound,
                ApplicationErrorCodes.Message.AttachmentNotFound,
                ApplicationErrorCodes.Message.DeleteForbidden);
    }

    private static async Task<IResult> HandleAsync(
        GuildChannelId channelId,
        MessageId messageId,
        UploadedFileId attachmentId,
        [FromServices] IAuthenticatedHandler<DeleteChannelMessageAttachmentInput, bool> handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var callerId = httpContext.GetRequiredAuthenticatedUserId();
        var response = await handler.HandleAsync(
            new DeleteChannelMessageAttachmentInput(channelId, messageId, attachmentId),
            callerId,
            cancellationToken);

        if (response.Success)
            return Results.NoContent();

        return response.ToHttpResult(httpContext);
    }
}
