using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects.Conversations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Conversations.GetConversationParticipants;

public static class GetConversationParticipantsEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/conversations/{conversationId}/participants", HandleAsync)
            .WithName("GetConversationParticipants")
            .WithTags("Conversations")
            .RequireAuthorization()
            .WithSummary("List participants of a conversation")
            .WithDescription("Returns the list of users currently in the conversation, with profile information.")
            .Produces<GetConversationParticipantsResponse>(StatusCodes.Status200OK)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Conversation.NotFound,
                ApplicationErrorCodes.Conversation.AccessDenied);
    }

    private static async Task<IResult> HandleAsync(
        ConversationId conversationId,
        [FromServices] IAuthenticatedHandler<ConversationId, GetConversationParticipantsResponse> handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var currentUserId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(conversationId, currentUserId, cancellationToken);
        return response.ToHttpResult(httpContext);
    }
}
