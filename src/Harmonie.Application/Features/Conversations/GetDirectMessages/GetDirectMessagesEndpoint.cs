using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Conversations.GetDirectMessages;

public static class GetDirectMessagesEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/conversations/{conversationId}/messages", HandleAsync)
            .WithName("GetDirectMessages")
            .WithTags("Conversations")
            .RequireAuthorization()
            .WithSummary("Get direct messages")
            .WithDescription("Returns direct messages in a conversation with cursor pagination.")
            .Produces<GetDirectMessagesResponse>(StatusCodes.Status200OK)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Conversation.NotFound,
                ApplicationErrorCodes.Conversation.AccessDenied);
    }

    private static async Task<IResult> HandleAsync(
        [AsParameters] GetDirectMessagesRouteRequest routeRequest,
        [AsParameters] GetDirectMessagesRequest request,
        [FromServices] GetDirectMessagesHandler handler,
        [FromServices] IValidator<GetDirectMessagesRouteRequest> routeValidator,
        [FromServices] IValidator<GetDirectMessagesRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var routeValidationError = await routeRequest.ValidateAsync(routeValidator, cancellationToken);
        if (routeValidationError is not null)
            return ApplicationResponse<GetDirectMessagesResponse>.Fail(routeValidationError).ToHttpResult();

        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<GetDirectMessagesResponse>.Fail(validationError).ToHttpResult();

        if (routeRequest.ConversationId is not string conversationId
            || !ConversationId.TryParse(conversationId, out var parsedConversationId)
            || parsedConversationId is null)
        {
            return ApplicationResponse<GetDirectMessagesResponse>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Route validation succeeded but conversation ID parsing failed.").ToHttpResult();
        }

        if (!httpContext.TryGetAuthenticatedUserId(out var currentUserId) || currentUserId is null)
        {
            return ApplicationResponse<GetDirectMessagesResponse>.Fail(
                    ApplicationErrorCodes.Auth.InvalidCredentials,
                    "Authenticated user identifier is missing.")
                .ToHttpResult();
        }

        var response = await handler.HandleAsync(parsedConversationId, request, currentUserId, cancellationToken);
        return response.ToHttpResult();
    }
}
