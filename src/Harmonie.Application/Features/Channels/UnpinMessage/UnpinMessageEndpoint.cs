using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Messages;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Channels.UnpinMessage;

public static class UnpinMessageEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/channels/{channelId}/messages/{messageId}/pin", HandleAsync)
            .WithName("UnpinChannelMessage")
            .WithTags("Channels")
            .RequireAuthorization()
            .WithSummary("Unpin a message in a channel")
            .WithDescription("Unpins a message in a channel. Idempotent — unpinning a non-pinned message is a no-op.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Channel.NotFound,
                ApplicationErrorCodes.Channel.NotText,
                ApplicationErrorCodes.Channel.AccessDenied,
                ApplicationErrorCodes.Pin.MessageNotFound);
    }

    private static async Task<IResult> HandleAsync(
        GuildChannelId channelId,
        MessageId messageId,
        [FromServices] IAuthenticatedHandler<ChannelUnpinMessageInput, bool> handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var callerId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(
            new ChannelUnpinMessageInput(channelId, messageId),
            callerId,
            cancellationToken);

        if (response.Success)
            return Results.NoContent();

        return response.ToHttpResult(httpContext);
    }
}
