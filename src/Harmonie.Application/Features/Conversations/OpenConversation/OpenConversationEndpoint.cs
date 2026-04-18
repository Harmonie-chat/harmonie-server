using FluentValidation;
using Harmonie.Application.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Conversations.OpenConversation;

public static class OpenConversationEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/conversations", HandleAsync)
            .WithName("OpenConversation")
            .WithTags("Conversations")
            .RequireAuthorization()
            .WithSummary("Open or get a direct conversation")
            .WithDescription("Creates a direct conversation between the authenticated user and the target user, or returns the existing one.")
            .Produces<OpenConversationResponse>(StatusCodes.Status201Created)
            .Produces<OpenConversationResponse>(StatusCodes.Status200OK)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Conversation.CannotOpenSelf,
                ApplicationErrorCodes.User.NotFound);
    }

    private static async Task<IResult> HandleAsync(
        [FromBody] OpenConversationRequest request,
        [FromServices] IAuthenticatedHandler<OpenConversationRequest, OpenConversationResponse> handler,
        [FromServices] IValidator<OpenConversationRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<OpenConversationResponse>.Fail(validationError).ToHttpResult(httpContext);

        var currentUserId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(request, currentUserId, cancellationToken);
        if (!response.Success)
            return response.ToHttpResult(httpContext);

        if (response.Data is null)
        {
            return ApplicationResponse<OpenConversationResponse>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Operation succeeded but no payload was returned.")
                .ToHttpResult(httpContext);
        }

        return response.Data.Created
            ? Results.Created($"/api/conversations/{response.Data.ConversationId}", response.Data)
            : Results.Ok(response.Data);
    }
}
