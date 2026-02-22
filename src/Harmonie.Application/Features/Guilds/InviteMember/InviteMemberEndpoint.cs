using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Guilds.InviteMember;

public static class InviteMemberEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/guilds/{guildId}/members/invite", HandleAsync)
            .WithName("InviteGuildMember")
            .WithTags("Guilds")
            .RequireAuthorization()
            .WithSummary("Invite a guild member")
            .WithDescription("Invites an existing user in the guild with the Member role.")
            .Produces<InviteMemberResponse>(StatusCodes.Status200OK)
            .Produces<ApplicationError>(StatusCodes.Status400BadRequest)
            .Produces<ApplicationError>(StatusCodes.Status401Unauthorized)
            .Produces<ApplicationError>(StatusCodes.Status403Forbidden)
            .Produces<ApplicationError>(StatusCodes.Status404NotFound)
            .Produces<ApplicationError>(StatusCodes.Status409Conflict)
            .Produces<ApplicationError>(StatusCodes.Status500InternalServerError);
    }

    private static async Task<IResult> HandleAsync(
        [FromRoute] string guildId,
        [FromBody] InviteMemberRequest request,
        [FromServices] InviteMemberHandler handler,
        [FromServices] IValidator<InviteMemberRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<InviteMemberResponse>.Fail(validationError).ToHttpResult();

        if (!GuildId.TryParse(guildId, out var parsedGuildId) || parsedGuildId is null)
        {
            var details = new Dictionary<string, string[]>
            {
                ["guildId"] = ["Guild ID must be a valid non-empty GUID"]
            };

            return ApplicationResponse<InviteMemberResponse>.Fail(
                ApplicationErrorCodes.Common.ValidationFailed,
                "Request validation failed",
                details).ToHttpResult();
        }

        if (!httpContext.TryGetAuthenticatedUserId(out var currentUserId) || currentUserId is null)
        {
            return ApplicationResponse<InviteMemberResponse>.Fail(
                    ApplicationErrorCodes.Auth.InvalidCredentials,
                    "Authenticated user identifier is missing.")
                .ToHttpResult();
        }

        var response = await handler.HandleAsync(parsedGuildId, request, currentUserId, cancellationToken);
        return response.ToHttpResult();
    }
}
