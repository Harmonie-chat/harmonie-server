using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Channels.DeleteMessage;

public static class DeleteMessageEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/channels/{channelId}/messages/{messageId}", HandleAsync)
            .WithName("DeleteChannelMessage")
            .WithTags("Channels")
            .RequireAuthorization()
            .WithSummary("Delete a message")
            .WithDescription("Soft-deletes a message. The message author can delete their own messages. Guild admins can delete any message.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Guild.AccessDenied,
                ApplicationErrorCodes.Channel.NotFound,
                ApplicationErrorCodes.Channel.NotText,
                ApplicationErrorCodes.Channel.AccessDenied,
                ApplicationErrorCodes.Message.NotFound,
                ApplicationErrorCodes.Message.DeleteForbidden);
    }

    private static async Task<IResult> HandleAsync(
        [AsParameters] DeleteMessageRouteRequest routeRequest,
        [FromServices] DeleteMessageHandler handler,
        [FromServices] IValidator<DeleteMessageRouteRequest> routeValidator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var routeValidationError = await routeRequest.ValidateAsync(routeValidator, cancellationToken);
        if (routeValidationError is not null)
            return ApplicationResponse<bool>.Fail(routeValidationError).ToHttpResult();

        if (routeRequest.ChannelId is not string channelIdStr
            || !GuildChannelId.TryParse(channelIdStr, out var parsedChannelId)
            || parsedChannelId is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Route validation succeeded but channel ID parsing failed.").ToHttpResult();
        }

        if (routeRequest.MessageId is not string messageIdStr
            || !ChannelMessageId.TryParse(messageIdStr, out var parsedMessageId)
            || parsedMessageId is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Route validation succeeded but message ID parsing failed.").ToHttpResult();
        }

        if (!httpContext.TryGetAuthenticatedUserId(out var callerId) || callerId is null)
        {
            return ApplicationResponse<bool>.Fail(
                    ApplicationErrorCodes.Auth.InvalidCredentials,
                    "Authenticated user identifier is missing.")
                .ToHttpResult();
        }

        var response = await handler.HandleAsync(parsedChannelId, parsedMessageId, callerId, cancellationToken);

        if (response.Success)
            return Results.NoContent();

        return response.ToHttpResult();
    }
}
