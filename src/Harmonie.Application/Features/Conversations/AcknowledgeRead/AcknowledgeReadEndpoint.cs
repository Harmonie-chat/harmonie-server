using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Conversations.AcknowledgeRead;

public static class AcknowledgeReadEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/conversations/{conversationId}/ack", HandleAsync)
            .WithName("AcknowledgeConversationRead")
            .WithTags("Conversations")
            .RequireAuthorization()
            .WithSummary("Mark a conversation as read")
            .WithDescription("Mark a conversation as read up to a specific message. If no message ID is provided, marks all messages as read.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Conversation.NotFound,
                ApplicationErrorCodes.Conversation.AccessDenied,
                ApplicationErrorCodes.Message.NotFound);
    }

    private static async Task<IResult> HandleAsync(
        [AsParameters] AcknowledgeReadRouteRequest routeRequest,
        [FromBody] AcknowledgeReadRequest request,
        [FromServices] AcknowledgeReadHandler handler,
        [FromServices] IValidator<AcknowledgeReadRouteRequest> routeValidator,
        [FromServices] IValidator<AcknowledgeReadRequest> bodyValidator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var routeValidationError = await routeRequest.ValidateAsync(routeValidator, cancellationToken);
        if (routeValidationError is not null)
            return ApplicationResponse<bool>.Fail(routeValidationError).ToHttpResult();

        var bodyValidationError = await request.ValidateAsync(bodyValidator, cancellationToken);
        if (bodyValidationError is not null)
            return ApplicationResponse<bool>.Fail(bodyValidationError).ToHttpResult();

        if (routeRequest.ConversationId is not string conversationIdStr
            || !ConversationId.TryParse(conversationIdStr, out var parsedConversationId)
            || parsedConversationId is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Route validation succeeded but conversation ID parsing failed.").ToHttpResult();
        }

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

        var response = await handler.HandleAsync(parsedConversationId, parsedMessageId, callerId, cancellationToken);

        if (response.Success)
            return Results.NoContent();

        return response.ToHttpResult();
    }
}
