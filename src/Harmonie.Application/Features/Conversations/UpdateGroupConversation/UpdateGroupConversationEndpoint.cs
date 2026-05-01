using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects.Conversations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Conversations.UpdateGroupConversation;

public static class UpdateGroupConversationEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/conversations/{conversationId}", HandleAsync)
            .WithName("UpdateGroupConversation")
            .WithTags("Conversations")
            .RequireAuthorization()
            .WithSummary("Update a group conversation")
            .WithDescription("Updates the name of a group conversation. Only participants can update group conversations. Direct conversations cannot be updated.")
            .Produces<UpdateGroupConversationResponse>(StatusCodes.Status200OK)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Common.DomainRuleViolation,
                ApplicationErrorCodes.Conversation.NotFound,
                ApplicationErrorCodes.Conversation.AccessDenied,
                ApplicationErrorCodes.Conversation.InvalidConversationType);
    }

    private static async Task<IResult> HandleAsync(
        ConversationId conversationId,
        [FromBody] UpdateGroupConversationRequest request,
        [FromServices] IAuthenticatedHandler<UpdateGroupConversationInput, UpdateGroupConversationResponse> handler,
        [FromServices] IValidator<UpdateGroupConversationRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<UpdateGroupConversationResponse>.Fail(validationError).ToHttpResult(httpContext);

        var callerId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(
            new UpdateGroupConversationInput(conversationId, request.Name),
            callerId,
            cancellationToken);
        return response.ToHttpResult(httpContext);
    }
}
