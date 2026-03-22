using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects.Guilds;
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
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Common.DomainRuleViolation,
                ApplicationErrorCodes.Guild.NotFound,
                ApplicationErrorCodes.Guild.InviteForbidden,
                ApplicationErrorCodes.Guild.InviteTargetNotFound,
                ApplicationErrorCodes.Guild.MemberAlreadyExists);
    }

    private static async Task<IResult> HandleAsync(
        GuildId guildId,
        [FromBody] InviteMemberRequest request,
        [FromServices] InviteMemberHandler handler,
        [FromServices] IValidator<InviteMemberRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<InviteMemberResponse>.Fail(validationError).ToHttpResult();

        var currentUserId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(guildId, request, currentUserId, cancellationToken);
        return response.ToHttpResult();
    }
}
