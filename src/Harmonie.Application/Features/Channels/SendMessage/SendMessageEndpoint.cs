using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Channels.SendMessage;

public static class SendMessageEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/channels/{channelId}/messages", HandleAsync)
            .WithName("SendChannelMessage")
            .WithTags("Channels")
            .RequireAuthorization()
            .RequireRateLimiting("message-post")
            .WithSummary("Send a message")
            .WithDescription("Posts a message in a text channel.")
            .Produces<SendMessageResponse>(StatusCodes.Status201Created)
            .Produces<ApplicationError>(StatusCodes.Status400BadRequest)
            .Produces<ApplicationError>(StatusCodes.Status401Unauthorized)
            .Produces<ApplicationError>(StatusCodes.Status403Forbidden)
            .Produces<ApplicationError>(StatusCodes.Status404NotFound)
            .Produces<ApplicationError>(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status429TooManyRequests)
            .Produces<ApplicationError>(StatusCodes.Status500InternalServerError);
    }

    private static async Task<IResult> HandleAsync(
        [FromRoute] string channelId,
        [FromBody] SendMessageRequest request,
        [FromServices] SendMessageHandler handler,
        [FromServices] IValidator<SendMessageRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<SendMessageResponse>.Fail(validationError).ToHttpResult();

        if (!GuildChannelId.TryParse(channelId, out var parsedChannelId) || parsedChannelId is null)
        {
            var details = new Dictionary<string, string[]>
            {
                ["channelId"] = ["Channel ID must be a valid non-empty GUID"]
            };

            return ApplicationResponse<SendMessageResponse>.Fail(
                ApplicationErrorCodes.Common.ValidationFailed,
                "Request validation failed",
                details).ToHttpResult();
        }

        if (!httpContext.TryGetAuthenticatedUserId(out var currentUserId) || currentUserId is null)
        {
            return ApplicationResponse<SendMessageResponse>.Fail(
                    ApplicationErrorCodes.Auth.InvalidCredentials,
                    "Authenticated user identifier is missing.")
                .ToHttpResult();
        }

        var response = await handler.HandleAsync(parsedChannelId, request, currentUserId, cancellationToken);
        return response.ToCreatedHttpResult(data => $"/api/channels/{data.ChannelId}/messages/{data.MessageId}");
    }
}
