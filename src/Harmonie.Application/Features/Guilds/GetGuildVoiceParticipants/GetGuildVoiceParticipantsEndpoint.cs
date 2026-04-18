using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects.Guilds;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Guilds.GetGuildVoiceParticipants;

public static class GetGuildVoiceParticipantsEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/guilds/{guildId}/voice/participants", HandleAsync)
            .WithName("GetGuildVoiceParticipants")
            .WithTags("Guilds")
            .RequireAuthorization()
            .WithSummary("List active voice participants for a guild")
            .WithDescription("Returns active LiveKit participants grouped by voice channel for an authenticated guild member.")
            .Produces<GetGuildVoiceParticipantsResponse>(StatusCodes.Status200OK)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Guild.NotFound,
                ApplicationErrorCodes.Guild.AccessDenied);
    }

    private static async Task<IResult> HandleAsync(
        GuildId guildId,
        [FromServices] IAuthenticatedHandler<GuildId, GetGuildVoiceParticipantsResponse> handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var currentUserId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(guildId, currentUserId, cancellationToken);
        return response.ToHttpResult(httpContext);
    }
}
