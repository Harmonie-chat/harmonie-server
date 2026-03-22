using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects.Channels;
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
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Guild.AccessDenied,
                ApplicationErrorCodes.Channel.NotFound,
                ApplicationErrorCodes.Channel.NotText,
                ApplicationErrorCodes.Channel.AccessDenied);
    }

    private static async Task<IResult> HandleAsync(
        GuildChannelId channelId,
        [AsParameters] GetMessagesRequest request,
        [FromServices] IAuthenticatedHandler<GetChannelMessagesInput, GetMessagesResponse> handler,
        [FromServices] IValidator<GetMessagesRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<GetMessagesResponse>.Fail(validationError).ToHttpResult();

        var currentUserId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(new GetChannelMessagesInput(channelId, request), currentUserId, cancellationToken);
        return response.ToHttpResult();
    }
}
