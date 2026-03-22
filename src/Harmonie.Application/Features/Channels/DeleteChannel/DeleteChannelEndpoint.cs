using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Channels.DeleteChannel;

public static class DeleteChannelEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/channels/{channelId}", HandleAsync)
            .WithName("DeleteChannel")
            .WithTags("Channels")
            .RequireAuthorization()
            .WithSummary("Delete a guild channel")
            .WithDescription("Deletes a guild channel. Only guild admins can delete channels. The default channel cannot be deleted.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Guild.AccessDenied,
                ApplicationErrorCodes.Channel.NotFound,
                ApplicationErrorCodes.Channel.AccessDenied,
                ApplicationErrorCodes.Channel.CannotDeleteDefault);
    }

    private static async Task<IResult> HandleAsync(
        GuildChannelId channelId,
        [FromServices] IAuthenticatedHandler<GuildChannelId, bool> handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var callerId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(channelId, callerId, cancellationToken);

        if (response.Success)
            return Results.NoContent();

        return response.ToHttpResult();
    }
}
