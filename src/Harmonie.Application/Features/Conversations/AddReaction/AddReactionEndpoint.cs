using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Conversations.AddReaction;

public static class AddReactionEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/conversations/{conversationId}/messages/{messageId}/reactions/{emoji}", HandleAsync)
            .WithName("AddConversationMessageReaction")
            .WithTags("Conversations")
            .RequireAuthorization()
            .WithSummary("Add a reaction to a conversation message")
            .WithDescription("Adds an emoji reaction to a conversation message. Idempotent — adding the same reaction twice is a no-op.")
            .Produces(StatusCodes.Status204NoContent)
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
        [AsParameters] AddReactionRouteRequest routeRequest,
        [FromServices] IAuthenticatedHandler<ConversationAddReactionInput, bool> handler,
        [FromServices] IValidator<AddReactionRouteRequest> routeValidator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var routeValidationError = await routeRequest.ValidateAsync(routeValidator, cancellationToken);
        if (routeValidationError is not null)
            return ApplicationResponse<bool>.Fail(routeValidationError).ToHttpResult(httpContext);

        if (routeRequest.Emoji is not string emoji)
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Route validation succeeded but emoji was null.").ToHttpResult(httpContext);

        var callerId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(new ConversationAddReactionInput(conversationId, messageId, emoji), callerId, cancellationToken);

        if (response.Success)
            return Results.NoContent();

        return response.ToHttpResult(httpContext);
    }
}
