using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects.Conversations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Conversations.GetPinnedMessages;

public static class GetPinnedMessagesEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/conversations/{conversationId}/pins", HandleAsync)
            .WithName("GetConversationPinnedMessages")
            .WithTags("Conversations")
            .RequireAuthorization()
            .WithSummary("List pinned messages in a conversation")
            .WithDescription("Returns all pinned messages for a conversation ordered by pinned date descending. Single-page response (no cursor).")
            .Produces<GetConversationPinnedMessagesResponse>()
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Conversation.NotFound,
                ApplicationErrorCodes.Conversation.AccessDenied);
    }

    private static async Task<IResult> HandleAsync(
        ConversationId conversationId,
        [FromServices] IAuthenticatedHandler<GetConversationPinnedMessagesInput, GetConversationPinnedMessagesResponse> handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var callerId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(
            new GetConversationPinnedMessagesInput(conversationId),
            callerId,
            cancellationToken);

        if (response.Success)
            return Results.Ok(response.Data);

        return response.ToHttpResult(httpContext);
    }
}
