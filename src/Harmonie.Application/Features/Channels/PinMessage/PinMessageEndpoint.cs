using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Messages;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Channels.PinMessage;

public static class PinMessageEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/channels/{channelId}/messages/{messageId}/pin", HandleAsync)
            .WithName("PinChannelMessage")
            .WithTags("Channels")
            .RequireAuthorization()
            .WithSummary("Pin a message in a channel")
            .WithDescription("Pins a message in a channel. Idempotent — pinning an already-pinned message is a no-op.")
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
        [FromServices] IAuthenticatedHandler<ChannelPinMessageInput, bool> handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var callerId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(
            new ChannelPinMessageInput(channelId, messageId),
            callerId,
            cancellationToken);

        if (response.Success)
            return Results.NoContent();

        return response.ToHttpResult(httpContext);
    }
}
