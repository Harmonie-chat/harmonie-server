using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Messages;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Channels.RemoveReaction;

public static class RemoveReactionEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/channels/{channelId}/messages/{messageId}/reactions/{emoji}", HandleAsync)
            .WithName("RemoveChannelMessageReaction")
            .WithTags("Channels")
            .RequireAuthorization()
            .WithSummary("Remove a reaction from a channel message")
            .WithDescription("Removes the caller's emoji reaction from a message. Idempotent — removing a non-existent reaction is a no-op.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Channel.NotFound,
                ApplicationErrorCodes.Channel.NotText,
                ApplicationErrorCodes.Channel.AccessDenied,
                ApplicationErrorCodes.Reaction.MessageNotFound);
    }

    private static async Task<IResult> HandleAsync(
        GuildChannelId channelId,
        MessageId messageId,
        [AsParameters] RemoveReactionRouteRequest routeRequest,
        [FromServices] RemoveReactionHandler handler,
        [FromServices] IValidator<RemoveReactionRouteRequest> routeValidator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var routeValidationError = await routeRequest.ValidateAsync(routeValidator, cancellationToken);
        if (routeValidationError is not null)
            return ApplicationResponse<bool>.Fail(routeValidationError).ToHttpResult();

        if (routeRequest.Emoji is not string emoji)
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Route validation succeeded but emoji was null.").ToHttpResult();

        var callerId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(channelId, messageId, emoji, callerId, cancellationToken);

        if (response.Success)
            return Results.NoContent();

        return response.ToHttpResult();
    }
}
