using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Guilds.ListGuildInvites;

public static class ListGuildInvitesEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/guilds/{guildId}/invites", HandleAsync)
            .WithName("ListGuildInvites")
            .WithTags("Guilds")
            .RequireAuthorization()
            .WithSummary("List guild invite links")
            .WithDescription("Returns all invite links for a guild, including expired ones. Admin only.")
            .Produces<ListGuildInvitesResponse>(StatusCodes.Status200OK)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Guild.NotFound,
                ApplicationErrorCodes.Guild.InviteForbidden);
    }

    private static async Task<IResult> HandleAsync(
        [AsParameters] ListGuildInvitesRequest request,
        [FromServices] ListGuildInvitesHandler handler,
        [FromServices] IValidator<ListGuildInvitesRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<ListGuildInvitesResponse>.Fail(validationError).ToHttpResult();

        if (request.GuildId is not string guildId
            || !GuildId.TryParse(guildId, out var parsedGuildId)
            || parsedGuildId is null)
        {
            return ApplicationResponse<ListGuildInvitesResponse>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Route validation succeeded but guild ID parsing failed.").ToHttpResult();
        }

        var currentUserId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(parsedGuildId, currentUserId, cancellationToken);
        return response.ToHttpResult();
    }
}
