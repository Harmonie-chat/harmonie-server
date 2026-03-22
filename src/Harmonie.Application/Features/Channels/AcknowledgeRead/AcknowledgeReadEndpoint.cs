using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Messages;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Channels.AcknowledgeRead;

public static class AcknowledgeReadEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/channels/{channelId}/ack", HandleAsync)
            .WithName("AcknowledgeChannelRead")
            .WithTags("Channels")
            .RequireAuthorization()
            .WithSummary("Mark a channel as read")
            .WithDescription("Mark a channel as read up to a specific message. If no message ID is provided, marks all messages as read.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Channel.NotFound,
                ApplicationErrorCodes.Channel.NotText,
                ApplicationErrorCodes.Channel.AccessDenied,
                ApplicationErrorCodes.Message.NotFound);
    }

    private static async Task<IResult> HandleAsync(
        GuildChannelId channelId,
        [FromBody] AcknowledgeReadRequest request,
        [FromServices] IAuthenticatedHandler<AcknowledgeChannelReadInput, bool> handler,
        [FromServices] IValidator<AcknowledgeReadRequest> bodyValidator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var bodyValidationError = await request.ValidateAsync(bodyValidator, cancellationToken);
        if (bodyValidationError is not null)
            return ApplicationResponse<bool>.Fail(bodyValidationError).ToHttpResult();

        MessageId? parsedMessageId = null;
        if (request.MessageId is string messageIdStr)
        {
            if (!MessageId.TryParse(messageIdStr, out var parsed) || parsed is null)
            {
                return ApplicationResponse<bool>.Fail(
                    ApplicationErrorCodes.Common.InvalidState,
                    "Body validation succeeded but message ID parsing failed.").ToHttpResult();
            }

            parsedMessageId = parsed;
        }

        var callerId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(new AcknowledgeChannelReadInput(channelId, parsedMessageId), callerId, cancellationToken);

        if (response.Success)
            return Results.NoContent();

        return response.ToHttpResult();
    }
}
