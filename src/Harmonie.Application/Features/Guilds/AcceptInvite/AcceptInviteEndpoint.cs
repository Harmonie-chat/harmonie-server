using FluentValidation;
using Harmonie.Application.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Guilds.AcceptInvite;

public static class AcceptInviteEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/invites/{inviteCode}/accept", HandleAsync)
            .WithName("AcceptInvite")
            .WithTags("Invites")
            .RequireAuthorization()
            .WithSummary("Accept an invite link")
            .WithDescription("Joins the guild associated with the invite. Validates the invite is still valid and adds the caller as a member.")
            .Produces<AcceptInviteResponse>(StatusCodes.Status200OK)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Common.DomainRuleViolation,
                ApplicationErrorCodes.Invite.NotFound,
                ApplicationErrorCodes.Invite.Expired,
                ApplicationErrorCodes.Invite.Exhausted,
                ApplicationErrorCodes.Guild.MemberAlreadyExists);
    }

    private static async Task<IResult> HandleAsync(
        [AsParameters] AcceptInviteRouteRequest routeRequest,
        [FromServices] IAuthenticatedHandler<string, AcceptInviteResponse> handler,
        [FromServices] IValidator<AcceptInviteRouteRequest> routeValidator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var routeValidationError = await routeRequest.ValidateAsync(routeValidator, cancellationToken);
        if (routeValidationError is not null)
            return ApplicationResponse<AcceptInviteResponse>.Fail(routeValidationError).ToHttpResult();

        var currentUserId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(routeRequest.InviteCode!, currentUserId, cancellationToken);
        return response.ToHttpResult();
    }
}
