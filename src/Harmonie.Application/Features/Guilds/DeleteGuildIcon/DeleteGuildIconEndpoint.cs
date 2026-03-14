using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Guilds.DeleteGuildIcon;

public static class DeleteGuildIconEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/guilds/{guildId}/icon", HandleAsync)
            .WithName("DeleteGuildIcon")
            .WithTags("Guilds")
            .RequireAuthorization()
            .WithSummary("Delete a guild icon")
            .WithDescription("Removes a guild's uploaded icon and falls back to the default icon. Only the guild owner or an admin can delete the guild icon.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Guild.NotFound,
                ApplicationErrorCodes.Guild.AccessDenied,
                ApplicationErrorCodes.Upload.NotFound);
    }

    private static async Task<IResult> HandleAsync(
        [AsParameters] DeleteGuildIconRouteRequest routeRequest,
        [FromServices] DeleteGuildIconHandler handler,
        [FromServices] IValidator<DeleteGuildIconRouteRequest> routeValidator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var routeValidationError = await routeRequest.ValidateAsync(routeValidator, cancellationToken);
        if (routeValidationError is not null)
            return ApplicationResponse<bool>.Fail(routeValidationError).ToHttpResult();

        if (routeRequest.GuildId is not string guildIdStr
            || !GuildId.TryParse(guildIdStr, out var parsedGuildId)
            || parsedGuildId is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Route validation succeeded but guild ID parsing failed.").ToHttpResult();
        }

        var callerId = httpContext.GetRequiredAuthenticatedUserId();
        var response = await handler.HandleAsync(parsedGuildId, callerId, cancellationToken);

        if (response.Success)
            return Results.NoContent();

        return response.ToHttpResult();
    }
}
