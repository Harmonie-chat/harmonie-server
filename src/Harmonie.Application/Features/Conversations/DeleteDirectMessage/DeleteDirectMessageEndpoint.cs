using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Conversations.DeleteDirectMessage;

public static class DeleteDirectMessageEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/conversations/{conversationId}/messages/{messageId}", HandleAsync)
            .WithName("DeleteDirectMessage")
            .WithTags("Conversations")
            .RequireAuthorization()
            .WithSummary("Delete a direct message")
            .WithDescription("Soft-deletes a direct message. Only the message author can delete their own direct messages.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Conversation.NotFound,
                ApplicationErrorCodes.Conversation.AccessDenied,
                ApplicationErrorCodes.Message.NotFound,
                ApplicationErrorCodes.Message.DeleteForbidden);
    }

    private static async Task<IResult> HandleAsync(
        [AsParameters] DeleteDirectMessageRouteRequest routeRequest,
        [FromServices] DeleteDirectMessageHandler handler,
        [FromServices] IValidator<DeleteDirectMessageRouteRequest> routeValidator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var routeValidationError = await routeRequest.ValidateAsync(routeValidator, cancellationToken);
        if (routeValidationError is not null)
            return ApplicationResponse<bool>.Fail(routeValidationError).ToHttpResult();

        if (routeRequest.ConversationId is not string conversationIdStr
            || !ConversationId.TryParse(conversationIdStr, out var parsedConversationId)
            || parsedConversationId is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Route validation succeeded but conversation ID parsing failed.").ToHttpResult();
        }

        if (routeRequest.MessageId is not string messageIdStr
            || !DirectMessageId.TryParse(messageIdStr, out var parsedMessageId)
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

        var response = await handler.HandleAsync(parsedConversationId, parsedMessageId, callerId, cancellationToken);
        if (response.Success)
            return Results.NoContent();

        return response.ToHttpResult();
    }
}
