using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Conversations.SearchConversationMessages;

public static class SearchConversationMessagesEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/conversations/{conversationId}/messages/search", HandleAsync)
            .WithName("SearchConversationMessages")
            .WithTags("Conversations")
            .RequireAuthorization()
            .WithSummary("Search direct messages")
            .WithDescription("Returns direct messages matching a full-text query with optional date filters and cursor pagination.")
            .Produces<SearchConversationMessagesResponse>(StatusCodes.Status200OK)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Conversation.NotFound,
                ApplicationErrorCodes.Conversation.AccessDenied);
    }

    private static async Task<IResult> HandleAsync(
        [AsParameters] SearchConversationMessagesRouteRequest routeRequest,
        [AsParameters] SearchConversationMessagesRequest request,
        [FromServices] SearchConversationMessagesHandler handler,
        [FromServices] IValidator<SearchConversationMessagesRouteRequest> routeValidator,
        [FromServices] IValidator<SearchConversationMessagesRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var routeValidationError = await routeRequest.ValidateAsync(routeValidator, cancellationToken);
        if (routeValidationError is not null)
            return ApplicationResponse<SearchConversationMessagesResponse>.Fail(routeValidationError).ToHttpResult();

        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<SearchConversationMessagesResponse>.Fail(validationError).ToHttpResult();

        if (routeRequest.ConversationId is not string conversationId
            || !ConversationId.TryParse(conversationId, out var parsedConversationId)
            || parsedConversationId is null)
        {
            return ApplicationResponse<SearchConversationMessagesResponse>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Route validation succeeded but conversation ID parsing failed.").ToHttpResult();
        }

        if (!httpContext.TryGetAuthenticatedUserId(out var currentUserId) || currentUserId is null)
        {
            return ApplicationResponse<SearchConversationMessagesResponse>.Fail(
                    ApplicationErrorCodes.Auth.InvalidCredentials,
                    "Authenticated user identifier is missing.")
                .ToHttpResult();
        }

        var response = await handler.HandleAsync(parsedConversationId, request, currentUserId, cancellationToken);
        return response.ToHttpResult();
    }
}
