using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects.Guilds;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Guilds.ReorderChannels;

public static class ReorderChannelsEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/guilds/{guildId}/channels/reorder", HandleAsync)
            .WithName("ReorderChannels")
            .WithTags("Guilds")
            .RequireAuthorization()
            .WithSummary("Reorder guild channels")
            .WithDescription("Bulk-updates the position of channels within a guild. Only guild admins can reorder channels.")
            .Produces<ReorderChannelsResponse>(StatusCodes.Status200OK)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Common.DomainRuleViolation,
                ApplicationErrorCodes.Guild.NotFound,
                ApplicationErrorCodes.Guild.AccessDenied,
                ApplicationErrorCodes.Channel.NotFound);
    }

    private static async Task<IResult> HandleAsync(
        GuildId guildId,
        [FromBody] ReorderChannelsRequest request,
        [FromServices] IAuthenticatedHandler<ReorderChannelsInput, ReorderChannelsResponse> handler,
        [FromServices] IValidator<ReorderChannelsRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<ReorderChannelsResponse>.Fail(validationError).ToHttpResult();

        var callerId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(new ReorderChannelsInput(guildId, request), callerId, cancellationToken);
        return response.ToHttpResult();
    }
}
