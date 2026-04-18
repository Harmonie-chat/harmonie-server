using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects.Conversations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Conversations.SendMessage;

public static class SendMessageEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/conversations/{conversationId}/messages", HandleAsync)
            .WithName("SendConversationMessage")
            .WithTags("Conversations")
            .RequireAuthorization()
            .RequireRateLimiting("message-post")
            .WithSummary("Send a conversation message")
            .WithDescription("Posts a message in a conversation where the authenticated user is a participant. Optional `attachmentFileIds` values must reference files previously uploaded with attachment purpose.")
            .Produces<SendMessageResponse>(StatusCodes.Status201Created)
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
        ConversationId conversationId,
        [FromBody] SendMessageRequest request,
        [FromServices] IAuthenticatedHandler<SendConversationMessageInput, SendMessageResponse> handler,
        [FromServices] IValidator<SendMessageRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<SendMessageResponse>.Fail(validationError).ToHttpResult(httpContext);

        var currentUserId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(new SendConversationMessageInput(conversationId, request.Content, request.AttachmentFileIds), currentUserId, cancellationToken);
        return response.ToCreatedHttpResult(
            data => $"/api/conversations/{data.ConversationId}/messages/{data.MessageId}", httpContext);
    }
}
