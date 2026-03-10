using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Conversations.EditDirectMessage;

public static class EditDirectMessageEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/conversations/{conversationId}/messages/{messageId}", HandleAsync)
            .WithName("EditDirectMessage")
            .WithTags("Conversations")
            .RequireAuthorization()
            .WithSummary("Edit a direct message")
            .WithDescription("Updates the content of a direct message. Only the message author can edit their own direct messages.")
            .Produces<EditDirectMessageResponse>(StatusCodes.Status200OK)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Common.DomainRuleViolation,
                ApplicationErrorCodes.Message.ContentEmpty,
                ApplicationErrorCodes.Message.ContentTooLong,
                ApplicationErrorCodes.Conversation.NotFound,
                ApplicationErrorCodes.Conversation.AccessDenied,
                ApplicationErrorCodes.Message.NotFound,
                ApplicationErrorCodes.Message.EditForbidden);
    }

    private static async Task<IResult> HandleAsync(
        [AsParameters] EditDirectMessageRouteRequest routeRequest,
        [FromBody] EditDirectMessageRequest request,
        [FromServices] EditDirectMessageHandler handler,
        [FromServices] IValidator<EditDirectMessageRouteRequest> routeValidator,
        [FromServices] IValidator<EditDirectMessageRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var routeValidationError = await routeRequest.ValidateAsync(routeValidator, cancellationToken);
        if (routeValidationError is not null)
            return ApplicationResponse<EditDirectMessageResponse>.Fail(routeValidationError).ToHttpResult();

        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<EditDirectMessageResponse>.Fail(validationError).ToHttpResult();

        if (routeRequest.ConversationId is not string conversationIdStr
            || !ConversationId.TryParse(conversationIdStr, out var parsedConversationId)
            || parsedConversationId is null)
        {
            return ApplicationResponse<EditDirectMessageResponse>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Route validation succeeded but conversation ID parsing failed.").ToHttpResult();
        }

        if (routeRequest.MessageId is not string messageIdStr
            || !DirectMessageId.TryParse(messageIdStr, out var parsedMessageId)
            || parsedMessageId is null)
        {
            return ApplicationResponse<EditDirectMessageResponse>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Route validation succeeded but message ID parsing failed.").ToHttpResult();
        }

        var callerId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(parsedConversationId, parsedMessageId, request, callerId, cancellationToken);
        return response.ToHttpResult();
    }
}
