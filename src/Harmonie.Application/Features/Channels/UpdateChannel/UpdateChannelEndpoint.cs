using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Channels.UpdateChannel;

public static class UpdateChannelEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/channels/{channelId}", HandleAsync)
            .WithName("UpdateChannel")
            .WithTags("Channels")
            .RequireAuthorization()
            .WithSummary("Update a guild channel")
            .WithDescription("Updates the name and/or position of a channel. Only guild admins can update channels.")
            .WithJsonRequestBodyDocumentation(
                "Partial channel update. Omit a field, or send it as null, to keep its current value.",
                (
                    "renameChannel",
                    "Rename a channel",
                    new
                    {
                        name = "announcements"
                    }),
                (
                    "moveChannel",
                    "Move a channel",
                    new
                    {
                        position = 3
                    }))
            .Produces<UpdateChannelResponse>(StatusCodes.Status200OK)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Common.DomainRuleViolation,
                ApplicationErrorCodes.Guild.AccessDenied,
                ApplicationErrorCodes.Channel.NotFound,
                ApplicationErrorCodes.Channel.AccessDenied,
                ApplicationErrorCodes.Channel.NameConflict);
    }

    private static async Task<IResult> HandleAsync(
        GuildChannelId channelId,
        [FromBody] UpdateChannelRequest request,
        [FromServices] UpdateChannelHandler handler,
        [FromServices] IValidator<UpdateChannelRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<UpdateChannelResponse>.Fail(validationError).ToHttpResult();

        var callerId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(channelId, callerId, request, cancellationToken);
        return response.ToHttpResult();
    }
}
