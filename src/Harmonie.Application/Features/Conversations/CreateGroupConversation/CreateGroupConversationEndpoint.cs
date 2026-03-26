using FluentValidation;
using Harmonie.Application.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Conversations.CreateGroupConversation;

public static class CreateGroupConversationEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/conversations/group", HandleAsync)
            .WithName("CreateGroupConversation")
            .WithTags("Conversations")
            .RequireAuthorization()
            .WithSummary("Create a group conversation")
            .WithDescription("Creates a new group conversation with the given participants. The authenticated user must be included in the participant list.")
            .Produces<CreateGroupConversationResponse>(StatusCodes.Status201Created)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Conversation.AccessDenied,
                ApplicationErrorCodes.User.NotFound);
    }

    private static async Task<IResult> HandleAsync(
        [FromBody] CreateGroupConversationRequest request,
        [FromServices] IAuthenticatedHandler<CreateGroupConversationRequest, CreateGroupConversationResponse> handler,
        [FromServices] IValidator<CreateGroupConversationRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<CreateGroupConversationResponse>.Fail(validationError).ToHttpResult();

        var currentUserId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(request, currentUserId, cancellationToken);
        if (!response.Success)
            return response.ToHttpResult();

        if (response.Data is null)
        {
            return ApplicationResponse<CreateGroupConversationResponse>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Operation succeeded but no payload was returned.")
                .ToHttpResult();
        }

        return Results.Created($"/api/conversations/{response.Data.ConversationId}", response.Data);
    }
}
