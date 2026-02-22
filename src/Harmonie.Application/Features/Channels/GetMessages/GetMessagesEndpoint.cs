using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Channels.GetMessages;

public static class GetMessagesEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/channels/{channelId}/messages", HandleAsync)
            .WithName("GetChannelMessages")
            .WithTags("Channels")
            .RequireAuthorization()
            .WithSummary("Get channel messages")
            .WithDescription("Returns channel messages with cursor pagination.")
            .Produces<GetMessagesResponse>(StatusCodes.Status200OK)
            .Produces<ApplicationError>(StatusCodes.Status400BadRequest)
            .Produces<ApplicationError>(StatusCodes.Status401Unauthorized)
            .Produces<ApplicationError>(StatusCodes.Status403Forbidden)
            .Produces<ApplicationError>(StatusCodes.Status404NotFound)
            .Produces<ApplicationError>(StatusCodes.Status409Conflict)
            .Produces<ApplicationError>(StatusCodes.Status500InternalServerError);
    }

    private static async Task<IResult> HandleAsync(
        [FromRoute] string channelId,
        [AsParameters] GetMessagesRequest request,
        [FromServices] GetMessagesHandler handler,
        [FromServices] IValidator<GetMessagesRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<GetMessagesResponse>.Fail(validationError).ToHttpResult();

        if (!GuildChannelId.TryParse(channelId, out var parsedChannelId) || parsedChannelId is null)
        {
            var details = new Dictionary<string, string[]>
            {
                ["channelId"] = ["Channel ID must be a valid non-empty GUID"]
            };

            return ApplicationResponse<GetMessagesResponse>.Fail(
                ApplicationErrorCodes.Common.ValidationFailed,
                "Request validation failed",
                details).ToHttpResult();
        }

        if (!httpContext.TryGetAuthenticatedUserId(out var currentUserId) || currentUserId is null)
        {
            return ApplicationResponse<GetMessagesResponse>.Fail(
                    ApplicationErrorCodes.Auth.InvalidCredentials,
                    "Authenticated user identifier is missing.")
                .ToHttpResult();
        }

        var response = await handler.HandleAsync(parsedChannelId, request, currentUserId, cancellationToken);
        return response.ToHttpResult();
    }
}
