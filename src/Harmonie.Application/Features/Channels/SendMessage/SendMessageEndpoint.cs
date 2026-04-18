using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects.Channels;
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
            .WithDescription("Posts a message in a text channel. Optional `attachmentFileIds` values must reference files previously uploaded with attachment purpose.")
            .Produces<SendMessageResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status429TooManyRequests)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Common.DomainRuleViolation,
                ApplicationErrorCodes.Message.ContentEmpty,
                ApplicationErrorCodes.Message.ContentTooLong,
                ApplicationErrorCodes.Guild.AccessDenied,
                ApplicationErrorCodes.Channel.NotFound,
                ApplicationErrorCodes.Channel.NotText,
                ApplicationErrorCodes.Channel.AccessDenied);
    }

    private static async Task<IResult> HandleAsync(
        GuildChannelId channelId,
        [FromBody] SendMessageRequest request,
        [FromServices] IAuthenticatedHandler<SendChannelMessageInput, SendMessageResponse> handler,
        [FromServices] IValidator<SendMessageRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<SendMessageResponse>.Fail(validationError).ToHttpResult(httpContext);

        var currentUserId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(new SendChannelMessageInput(channelId, request.Content, request.AttachmentFileIds), currentUserId, cancellationToken);
        return response.ToCreatedHttpResult(data => $"/api/channels/{data.ChannelId}/messages/{data.MessageId}", httpContext);
    }
}
