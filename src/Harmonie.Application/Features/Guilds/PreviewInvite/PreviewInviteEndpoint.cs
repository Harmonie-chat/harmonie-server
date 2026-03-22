using FluentValidation;
using Harmonie.Application.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Guilds.PreviewInvite;

public static class PreviewInviteEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/invites/{inviteCode}", HandleAsync)
            .WithName("PreviewInvite")
            .WithTags("Invites")
            .WithSummary("Preview an invite link")
            .WithDescription("Returns guild public info and invite usage without joining. No authentication required.")
            .Produces<PreviewInviteResponse>(StatusCodes.Status200OK)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Invite.NotFound,
                ApplicationErrorCodes.Invite.Expired,
                ApplicationErrorCodes.Invite.Exhausted);
    }

    private static async Task<IResult> HandleAsync(
        [AsParameters] PreviewInviteRouteRequest routeRequest,
        [FromServices] IHandler<string, PreviewInviteResponse> handler,
        [FromServices] IValidator<PreviewInviteRouteRequest> routeValidator,
        CancellationToken cancellationToken)
    {
        var routeValidationError = await routeRequest.ValidateAsync(routeValidator, cancellationToken);
        if (routeValidationError is not null)
            return ApplicationResponse<PreviewInviteResponse>.Fail(routeValidationError).ToHttpResult();

        var response = await handler.HandleAsync(routeRequest.InviteCode!, cancellationToken);
        return response.ToHttpResult();
    }
}
