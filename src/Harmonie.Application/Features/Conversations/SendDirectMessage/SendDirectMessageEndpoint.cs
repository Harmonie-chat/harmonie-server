using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Conversations.SendDirectMessage;

public static class SendDirectMessageEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/conversations/{conversationId}/messages", HandleAsync)
            .WithName("SendDirectMessage")
            .WithTags("Conversations")
            .RequireAuthorization()
            .RequireRateLimiting("message-post")
            .WithSummary("Send a direct message")
            .WithDescription("Posts a direct message in a conversation where the authenticated user is a participant.")
            .Produces<SendDirectMessageResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status429TooManyRequests)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Common.DomainRuleViolation,
                ApplicationErrorCodes.Message.ContentEmpty,
                ApplicationErrorCodes.Message.ContentTooLong,
                ApplicationErrorCodes.Conversation.NotFound,
                ApplicationErrorCodes.Conversation.AccessDenied);
    }

    private static async Task<IResult> HandleAsync(
        [AsParameters] SendDirectMessageRouteRequest routeRequest,
        [FromBody] SendDirectMessageRequest request,
        [FromServices] SendDirectMessageHandler handler,
        [FromServices] IValidator<SendDirectMessageRouteRequest> routeValidator,
        [FromServices] IValidator<SendDirectMessageRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var routeValidationError = await routeRequest.ValidateAsync(routeValidator, cancellationToken);
        if (routeValidationError is not null)
            return ApplicationResponse<SendDirectMessageResponse>.Fail(routeValidationError).ToHttpResult();

        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<SendDirectMessageResponse>.Fail(validationError).ToHttpResult();

        if (routeRequest.ConversationId is not string conversationId
            || !ConversationId.TryParse(conversationId, out var parsedConversationId)
            || parsedConversationId is null)
        {
            return ApplicationResponse<SendDirectMessageResponse>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Route validation succeeded but conversation ID parsing failed.").ToHttpResult();
        }

        if (!httpContext.TryGetAuthenticatedUserId(out var currentUserId) || currentUserId is null)
        {
            return ApplicationResponse<SendDirectMessageResponse>.Fail(
                    ApplicationErrorCodes.Auth.InvalidCredentials,
                    "Authenticated user identifier is missing.")
                .ToHttpResult();
        }

        var response = await handler.HandleAsync(parsedConversationId, request, currentUserId, cancellationToken);
        return response.ToCreatedHttpResult(
            data => $"/api/conversations/{data.ConversationId}/messages/{data.MessageId}");
    }
}
