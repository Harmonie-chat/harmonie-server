using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects;
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
            .Produces<ApplicationError>(StatusCodes.Status400BadRequest)
            .Produces<ApplicationError>(StatusCodes.Status401Unauthorized)
            .Produces<ApplicationError>(StatusCodes.Status403Forbidden)
            .Produces<ApplicationError>(StatusCodes.Status404NotFound)
            .Produces<ApplicationError>(StatusCodes.Status500InternalServerError);
    }

    private static async Task<IResult> HandleAsync(
        [FromRoute] string guildId,
        [FromServices] GetGuildChannelsHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!GuildId.TryParse(guildId, out var parsedGuildId) || parsedGuildId is null)
        {
            var details = new Dictionary<string, string[]>
            {
                ["guildId"] = ["Guild ID must be a valid non-empty GUID"]
            };

            return ApplicationResponse<GetGuildChannelsResponse>.Fail(
                ApplicationErrorCodes.Common.ValidationFailed,
                "Request validation failed",
                details).ToHttpResult();
        }

        if (!httpContext.TryGetAuthenticatedUserId(out var currentUserId) || currentUserId is null)
        {
            return ApplicationResponse<GetGuildChannelsResponse>.Fail(
                    ApplicationErrorCodes.Auth.InvalidCredentials,
                    "Authenticated user identifier is missing.")
                .ToHttpResult();
        }

        var response = await handler.HandleAsync(parsedGuildId, currentUserId, cancellationToken);
        return response.ToHttpResult();
    }
}
