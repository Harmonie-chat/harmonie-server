using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects.Guilds;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Guilds.GetGuildChannels;

public static class GetGuildChannelsEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/guilds/{guildId}/channels", HandleAsync)
            .WithName("GetGuildChannels")
            .WithTags("Guilds")
            .RequireAuthorization()
            .WithSummary("List guild channels")
            .WithDescription("Returns guild channels for an authenticated guild member.")
            .Produces<GetGuildChannelsResponse>(StatusCodes.Status200OK)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Guild.NotFound,
                ApplicationErrorCodes.Guild.AccessDenied);
    }

    private static async Task<IResult> HandleAsync(
        GuildId guildId,
        [FromServices] IAuthenticatedHandler<GuildId, GetGuildChannelsResponse> handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var currentUserId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(guildId, currentUserId, cancellationToken);
        return response.ToHttpResult();
    }
}
