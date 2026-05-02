using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Conversations.GetReactionUsers;

public static class GetReactionUsersEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/conversations/{conversationId}/messages/{messageId}/reactions/{emoji}/users", HandleAsync)
            .WithName("GetConversationMessageReactionUsers")
            .WithTags("Conversations")
            .RequireAuthorization()
            .WithSummary("Get users who reacted with a specific emoji on a conversation message")
            .WithDescription("Returns a paginated list of users who reacted with the given emoji on the specified message.")
            .Produces<GetReactionUsersResponse>()
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Conversation.NotFound,
                ApplicationErrorCodes.Conversation.AccessDenied,
                ApplicationErrorCodes.Reaction.MessageNotFound);
    }

    private static async Task<IResult> HandleAsync(
        ConversationId conversationId,
        MessageId messageId,
        [AsParameters] GetReactionUsersRouteRequest routeRequest,
        [FromServices] IAuthenticatedHandler<GetConversationReactionUsersInput, GetReactionUsersResponse> handler,
        [FromServices] IValidator<GetReactionUsersRouteRequest> routeValidator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var routeValidationError = await routeRequest.ValidateAsync(routeValidator, cancellationToken);
        if (routeValidationError is not null)
            return ApplicationResponse<GetReactionUsersResponse>.Fail(routeValidationError).ToHttpResult(httpContext);

        if (routeRequest.Emoji is not string emoji)
            return ApplicationResponse<GetReactionUsersResponse>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Route validation succeeded but emoji was null.").ToHttpResult(httpContext);

        var callerId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(
            new GetConversationReactionUsersInput(conversationId, messageId, emoji, routeRequest.Cursor, routeRequest.Limit),
            callerId,
            cancellationToken);

        return response.ToHttpResult(httpContext);
    }
}
