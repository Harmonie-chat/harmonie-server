using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Channels.EditMessage;

public static class EditMessageEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/channels/{channelId}/messages/{messageId}", HandleAsync)
            .WithName("EditChannelMessage")
            .WithTags("Channels")
            .RequireAuthorization()
            .WithSummary("Edit a message")
            .WithDescription("Updates the content of a message. Only the message author can edit their own messages.")
            .Produces<EditMessageResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesErrors(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                ApplicationErrorCodes.Message.ContentEmpty,
                ApplicationErrorCodes.Message.ContentTooLong,
                ApplicationErrorCodes.Guild.AccessDenied,
                ApplicationErrorCodes.Channel.NotFound,
                ApplicationErrorCodes.Channel.NotText,
                ApplicationErrorCodes.Channel.AccessDenied,
                ApplicationErrorCodes.Message.NotFound,
                ApplicationErrorCodes.Message.EditForbidden);
    }

    private static async Task<IResult> HandleAsync(
        [AsParameters] EditMessageRouteRequest routeRequest,
        [FromBody] EditMessageRequest request,
        [FromServices] EditMessageHandler handler,
        [FromServices] IValidator<EditMessageRouteRequest> routeValidator,
        [FromServices] IValidator<EditMessageRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var routeValidationError = await routeRequest.ValidateAsync(routeValidator, cancellationToken);
        if (routeValidationError is not null)
            return ApplicationResponse<EditMessageResponse>.Fail(routeValidationError).ToHttpResult();

        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<EditMessageResponse>.Fail(validationError).ToHttpResult();

        if (routeRequest.ChannelId is not string channelIdStr
            || !GuildChannelId.TryParse(channelIdStr, out var parsedChannelId)
            || parsedChannelId is null)
        {
            return ApplicationResponse<EditMessageResponse>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Route validation succeeded but channel ID parsing failed.").ToHttpResult();
        }

        if (routeRequest.MessageId is not string messageIdStr
            || !ChannelMessageId.TryParse(messageIdStr, out var parsedMessageId)
            || parsedMessageId is null)
        {
            return ApplicationResponse<EditMessageResponse>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Route validation succeeded but message ID parsing failed.").ToHttpResult();
        }

        if (!httpContext.TryGetAuthenticatedUserId(out var callerId) || callerId is null)
        {
            return ApplicationResponse<EditMessageResponse>.Fail(
                    ApplicationErrorCodes.Auth.InvalidCredentials,
                    "Authenticated user identifier is missing.")
                .ToHttpResult();
        }

        var response = await handler.HandleAsync(parsedChannelId, parsedMessageId, request, callerId, cancellationToken);
        return response.ToHttpResult();
    }
}
