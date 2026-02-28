using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Guilds.LeaveGuild;

public static class LeaveGuildEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/guilds/{guildId}/leave", HandleAsync)
            .WithName("LeaveGuild")
            .WithTags("Guilds")
            .RequireAuthorization()
            .WithSummary("Leave a guild")
            .WithDescription("Removes the authenticated user from the guild. The guild owner cannot leave.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ApplicationError>(StatusCodes.Status401Unauthorized)
            .Produces<ApplicationError>(StatusCodes.Status403Forbidden)
            .Produces<ApplicationError>(StatusCodes.Status404NotFound)
            .Produces<ApplicationError>(StatusCodes.Status409Conflict)
            .Produces<ApplicationError>(StatusCodes.Status500InternalServerError);
    }

    private static async Task<IResult> HandleAsync(
        [AsParameters] LeaveGuildRouteRequest routeRequest,
        [FromServices] LeaveGuildHandler handler,
        [FromServices] IValidator<LeaveGuildRouteRequest> routeValidator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var routeValidationError = await routeRequest.ValidateAsync(routeValidator, cancellationToken);
        if (routeValidationError is not null)
            return ApplicationResponse<bool>.Fail(routeValidationError).ToHttpResult();

        if (routeRequest.GuildId is not string guildId
            || !GuildId.TryParse(guildId, out var parsedGuildId)
            || parsedGuildId is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Route validation succeeded but guild ID parsing failed.").ToHttpResult();
        }

        if (!httpContext.TryGetAuthenticatedUserId(out var currentUserId) || currentUserId is null)
        {
            return ApplicationResponse<bool>.Fail(
                    ApplicationErrorCodes.Auth.InvalidCredentials,
                    "Authenticated user identifier is missing.")
                .ToHttpResult();
        }

        var response = await handler.HandleAsync(parsedGuildId, currentUserId, cancellationToken);

        if (response.Success)
            return Results.NoContent();

        return response.ToHttpResult();
    }
}
