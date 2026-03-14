using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Conversations.DeleteMessageAttachment;

public static class DeleteMessageAttachmentEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/conversations/{conversationId}/messages/{messageId}/attachments/{attachmentId}", HandleAsync)
            .WithName("DeleteConversationMessageAttachment")
            .WithTags("Conversations")
            .RequireAuthorization()
            .WithSummary("Delete a conversation message attachment")
            .WithDescription("Deletes a specific attachment from a conversation message. Only the message author can delete attachments from their own messages.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Conversation.NotFound,
                ApplicationErrorCodes.Conversation.AccessDenied,
                ApplicationErrorCodes.Message.NotFound,
                ApplicationErrorCodes.Message.AttachmentNotFound,
                ApplicationErrorCodes.Message.DeleteForbidden);
    }

    private static async Task<IResult> HandleAsync(
        [AsParameters] DeleteMessageAttachmentRouteRequest routeRequest,
        [FromServices] DeleteMessageAttachmentHandler handler,
        [FromServices] IValidator<DeleteMessageAttachmentRouteRequest> routeValidator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var routeValidationError = await routeRequest.ValidateAsync(routeValidator, cancellationToken);
        if (routeValidationError is not null)
            return ApplicationResponse<bool>.Fail(routeValidationError).ToHttpResult();

        if (routeRequest.ConversationId is not string conversationIdStr
            || !ConversationId.TryParse(conversationIdStr, out var parsedConversationId)
            || parsedConversationId is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Route validation succeeded but conversation ID parsing failed.").ToHttpResult();
        }

        if (routeRequest.MessageId is not string messageIdStr
            || !MessageId.TryParse(messageIdStr, out var parsedMessageId)
            || parsedMessageId is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Route validation succeeded but message ID parsing failed.").ToHttpResult();
        }

        if (routeRequest.AttachmentId is not string attachmentIdStr
            || !UploadedFileId.TryParse(attachmentIdStr, out var parsedAttachmentId)
            || parsedAttachmentId is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Route validation succeeded but attachment ID parsing failed.").ToHttpResult();
        }

        var callerId = httpContext.GetRequiredAuthenticatedUserId();
        var response = await handler.HandleAsync(
            parsedConversationId,
            parsedMessageId,
            parsedAttachmentId,
            callerId,
            cancellationToken);

        if (response.Success)
            return Results.NoContent();

        return response.ToHttpResult();
    }
}
