using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Messages;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Channels.AddReaction;

public static class AddReactionEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/channels/{channelId}/messages/{messageId}/reactions/{emoji}", HandleAsync)
            .WithName("AddChannelMessageReaction")
            .WithTags("Channels")
            .RequireAuthorization()
            .WithSummary("Add a reaction to a channel message")
            .WithDescription("Adds an emoji reaction to a message. Idempotent — adding the same reaction twice is a no-op.")
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
        [AsParameters] AddReactionRouteRequest routeRequest,
        [FromServices] AddReactionHandler handler,
        [FromServices] IValidator<AddReactionRouteRequest> routeValidator,
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
