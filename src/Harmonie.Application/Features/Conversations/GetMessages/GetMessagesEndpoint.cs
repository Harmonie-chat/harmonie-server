using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects.Conversations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Conversations.GetMessages;

public static class GetMessagesEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/conversations/{conversationId}/messages", HandleAsync)
            .WithName("GetConversationMessages")
            .WithTags("Conversations")
            .RequireAuthorization()
            .WithSummary("Get conversation messages")
            .WithDescription("Returns messages in a conversation with cursor pagination.")
            .Produces<GetMessagesResponse>(StatusCodes.Status200OK)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Conversation.NotFound,
                ApplicationErrorCodes.Conversation.AccessDenied);
    }

    private static async Task<IResult> HandleAsync(
        ConversationId conversationId,
        [AsParameters] GetMessagesRequest request,
        [FromServices] IAuthenticatedHandler<GetConversationMessagesInput, GetMessagesResponse> handler,
        [FromServices] IValidator<GetMessagesRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<GetMessagesResponse>.Fail(validationError).ToHttpResult();

        var currentUserId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(new GetConversationMessagesInput(conversationId, request), currentUserId, cancellationToken);
        return response.ToHttpResult();
    }
}
