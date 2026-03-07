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
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Guild.AccessDenied,
                ApplicationErrorCodes.Channel.NotFound,
                ApplicationErrorCodes.Channel.NotText,
                ApplicationErrorCodes.Channel.AccessDenied);
    }

    private static async Task<IResult> HandleAsync(
        [AsParameters] GetMessagesRouteRequest routeRequest,
        [AsParameters] GetMessagesRequest request,
        [FromServices] GetMessagesHandler handler,
        [FromServices] IValidator<GetMessagesRouteRequest> routeValidator,
        [FromServices] IValidator<GetMessagesRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var routeValidationError = await routeRequest.ValidateAsync(routeValidator, cancellationToken);
        if (routeValidationError is not null)
            return ApplicationResponse<GetMessagesResponse>.Fail(routeValidationError).ToHttpResult();

        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<GetMessagesResponse>.Fail(validationError).ToHttpResult();

        if (routeRequest.ChannelId is not string channelId
            || !GuildChannelId.TryParse(channelId, out var parsedChannelId)
            || parsedChannelId is null)
        {
            return ApplicationResponse<GetMessagesResponse>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Route validation succeeded but channel ID parsing failed.").ToHttpResult();
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
