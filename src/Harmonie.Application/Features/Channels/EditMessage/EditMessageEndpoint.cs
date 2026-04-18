using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Messages;
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
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
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
        GuildChannelId channelId,
        MessageId messageId,
        [FromBody] EditMessageRequest request,
        [FromServices] IAuthenticatedHandler<EditChannelMessageInput, EditMessageResponse> handler,
        [FromServices] IValidator<EditMessageRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<EditMessageResponse>.Fail(validationError).ToHttpResult(httpContext);

        var callerId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(new EditChannelMessageInput(channelId, messageId, request.Content), callerId, cancellationToken);
        return response.ToHttpResult(httpContext);
    }
}
