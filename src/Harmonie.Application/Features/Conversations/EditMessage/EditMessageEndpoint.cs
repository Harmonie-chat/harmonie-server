using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Conversations.EditMessage;

public static class EditMessageEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/conversations/{conversationId}/messages/{messageId}", HandleAsync)
            .WithName("EditConversationMessage")
            .WithTags("Conversations")
            .RequireAuthorization()
            .WithSummary("Edit a conversation message")
            .WithDescription("Updates the content of a conversation message. Only the message author can edit their own messages.")
            .Produces<EditMessageResponse>(StatusCodes.Status200OK)
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
        ConversationId conversationId,
        MessageId messageId,
        [FromBody] EditMessageRequest request,
        [FromServices] IAuthenticatedHandler<EditConversationMessageInput, EditMessageResponse> handler,
        [FromServices] IValidator<EditMessageRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<EditMessageResponse>.Fail(validationError).ToHttpResult(httpContext);

        var callerId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(new EditConversationMessageInput(conversationId, messageId, request.Content), callerId, cancellationToken);
        return response.ToHttpResult(httpContext);
    }
}
