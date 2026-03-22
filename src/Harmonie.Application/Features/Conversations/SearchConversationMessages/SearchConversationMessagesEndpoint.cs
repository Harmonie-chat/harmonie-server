using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects.Conversations;
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
            .WithSummary("Search conversation messages")
            .WithDescription("Returns conversation messages matching a full-text query with optional date filters and cursor pagination.")
            .Produces<SearchConversationMessagesResponse>(StatusCodes.Status200OK)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Conversation.NotFound,
                ApplicationErrorCodes.Conversation.AccessDenied);
    }

    private static async Task<IResult> HandleAsync(
        ConversationId conversationId,
        [AsParameters] SearchConversationMessagesRequest request,
        [FromServices] IAuthenticatedHandler<SearchConversationMessagesInput, SearchConversationMessagesResponse> handler,
        [FromServices] IValidator<SearchConversationMessagesRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<SearchConversationMessagesResponse>.Fail(validationError).ToHttpResult();

        var currentUserId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(new SearchConversationMessagesInput(conversationId, request), currentUserId, cancellationToken);
        return response.ToHttpResult();
    }
}
