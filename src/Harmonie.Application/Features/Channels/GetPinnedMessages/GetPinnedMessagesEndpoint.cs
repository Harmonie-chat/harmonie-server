using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Channels.GetPinnedMessages;

public static class GetPinnedMessagesEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/channels/{channelId}/pins", HandleAsync)
            .WithName("GetChannelPinnedMessages")
            .WithTags("Channels")
            .RequireAuthorization()
            .WithSummary("List pinned messages in a channel")
            .WithDescription("Returns all pinned messages for a channel ordered by pinned date descending. Single-page response (no cursor).")
            .Produces<GetPinnedMessagesResponse>()
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Channel.NotFound,
                ApplicationErrorCodes.Channel.NotText,
                ApplicationErrorCodes.Channel.AccessDenied);
    }

    private static async Task<IResult> HandleAsync(
        GuildChannelId channelId,
        [FromServices] IAuthenticatedHandler<GetChannelPinnedMessagesInput, GetPinnedMessagesResponse> handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var callerId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(
            new GetChannelPinnedMessagesInput(channelId),
            callerId,
            cancellationToken);

        if (response.Success)
            return Results.Ok(response.Data);

        return response.ToHttpResult(httpContext);
    }
}
