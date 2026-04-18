using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Messages;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Channels.DeleteMessage;

public static class DeleteMessageEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/channels/{channelId}/messages/{messageId}", HandleAsync)
            .WithName("DeleteChannelMessage")
            .WithTags("Channels")
            .RequireAuthorization()
            .WithSummary("Delete a message")
            .WithDescription("Soft-deletes a message. The message author can delete their own messages. Guild admins can delete any message.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Guild.AccessDenied,
                ApplicationErrorCodes.Channel.NotFound,
                ApplicationErrorCodes.Channel.NotText,
                ApplicationErrorCodes.Channel.AccessDenied,
                ApplicationErrorCodes.Message.NotFound,
                ApplicationErrorCodes.Message.DeleteForbidden);
    }

    private static async Task<IResult> HandleAsync(
        GuildChannelId channelId,
        MessageId messageId,
        [FromServices] IAuthenticatedHandler<DeleteChannelMessageInput, bool> handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var callerId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(new DeleteChannelMessageInput(channelId, messageId), callerId, cancellationToken);

        if (response.Success)
            return Results.NoContent();

        return response.ToHttpResult(httpContext);
    }
}
