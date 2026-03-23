using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Channels.JoinVoiceChannel;

public static class JoinVoiceChannelEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/channels/{channelId}/voice/join", HandleAsync)
            .WithName("JoinVoiceChannel")
            .WithTags("Channels")
            .RequireAuthorization()
            .WithSummary("Join a voice channel")
            .WithDescription("Returns a LiveKit access token for an authenticated guild member in a voice channel.")
            .Produces<JoinVoiceChannelResponse>(StatusCodes.Status200OK)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Channel.NotFound,
                ApplicationErrorCodes.Channel.NotVoice,
                ApplicationErrorCodes.Channel.AccessDenied,
                ApplicationErrorCodes.User.NotFound);
    }

    private static async Task<IResult> HandleAsync(
        GuildChannelId channelId,
        [FromServices] IAuthenticatedHandler<GuildChannelId, JoinVoiceChannelResponse> handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var currentUserId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(channelId, currentUserId, cancellationToken);
        return response.ToHttpResult();
    }
}
