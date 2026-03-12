using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Guilds.CreateGuildInvite;

public static class CreateGuildInviteEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/guilds/{guildId}/invites", HandleAsync)
            .WithName("CreateGuildInvite")
            .WithTags("Guilds")
            .RequireAuthorization()
            .WithSummary("Create a guild invite link")
            .WithDescription("Generates a unique invite link for a guild with optional expiration and max uses.")
            .Produces<CreateGuildInviteResponse>(StatusCodes.Status201Created)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Common.DomainRuleViolation,
                ApplicationErrorCodes.Guild.NotFound,
                ApplicationErrorCodes.Guild.InviteForbidden);
    }

    private static async Task<IResult> HandleAsync(
        [AsParameters] CreateGuildInviteRouteRequest routeRequest,
        [FromBody] CreateGuildInviteRequest request,
        [FromServices] CreateGuildInviteHandler handler,
        [FromServices] IValidator<CreateGuildInviteRouteRequest> routeValidator,
        [FromServices] IValidator<CreateGuildInviteRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var routeValidationError = await routeRequest.ValidateAsync(routeValidator, cancellationToken);
        if (routeValidationError is not null)
            return ApplicationResponse<CreateGuildInviteResponse>.Fail(routeValidationError).ToHttpResult();

        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<CreateGuildInviteResponse>.Fail(validationError).ToHttpResult();

        if (routeRequest.GuildId is not string guildIdStr
            || !GuildId.TryParse(guildIdStr, out var parsedGuildId)
            || parsedGuildId is null)
        {
            return ApplicationResponse<CreateGuildInviteResponse>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Route validation succeeded but guild ID parsing failed.").ToHttpResult();
        }

        var currentUserId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(parsedGuildId, request, currentUserId, cancellationToken);
        return response.ToCreatedHttpResult(data => $"/api/guilds/{data.GuildId}/invites/{data.InviteId}");
    }
}
