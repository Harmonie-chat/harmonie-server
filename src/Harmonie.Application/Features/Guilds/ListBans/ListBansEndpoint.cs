using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Guilds.ListBans;

public static class ListBansEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/guilds/{guildId}/bans", HandleAsync)
            .WithName("ListBans")
            .WithTags("Guilds")
            .RequireAuthorization()
            .WithSummary("List banned users in a guild")
            .WithDescription("Returns all banned users in a guild with ban reason and date. Admin only.")
            .Produces<ListBansResponse>(StatusCodes.Status200OK)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Guild.NotFound,
                ApplicationErrorCodes.Guild.AccessDenied);
    }

    private static async Task<IResult> HandleAsync(
        [AsParameters] ListBansRequest request,
        [FromServices] ListBansHandler handler,
        [FromServices] IValidator<ListBansRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<ListBansResponse>.Fail(validationError).ToHttpResult();

        if (request.GuildId is not string guildId
            || !GuildId.TryParse(guildId, out var parsedGuildId)
            || parsedGuildId is null)
        {
            return ApplicationResponse<ListBansResponse>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Route validation succeeded but guild ID parsing failed.").ToHttpResult();
        }

        var currentUserId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(parsedGuildId, currentUserId, cancellationToken);
        return response.ToHttpResult();
    }
}
