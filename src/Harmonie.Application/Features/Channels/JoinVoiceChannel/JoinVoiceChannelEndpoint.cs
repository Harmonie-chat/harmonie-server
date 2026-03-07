using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Channels.JoinVoiceChannel;

public static class JoinVoiceChannelEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/channels/{channelId}/voice/join", HandleAsync)
            .WithName("JoinVoiceChannel")
            .WithTags("Channels")
            .RequireAuthorization()
            .WithSummary("Join a voice channel")
            .WithDescription("Returns a LiveKit access token for an authenticated guild member in a voice channel.")
            .Produces<JoinVoiceChannelResponse>(StatusCodes.Status200OK)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Channel.NotFound,
                ApplicationErrorCodes.Channel.NotVoice,
                ApplicationErrorCodes.Channel.AccessDenied,
                ApplicationErrorCodes.User.NotFound);
    }

    private static async Task<IResult> HandleAsync(
        [AsParameters] JoinVoiceChannelRouteRequest routeRequest,
        [FromServices] JoinVoiceChannelHandler handler,
        [FromServices] IValidator<JoinVoiceChannelRouteRequest> routeValidator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var routeValidationError = await routeRequest.ValidateAsync(routeValidator, cancellationToken);
        if (routeValidationError is not null)
            return ApplicationResponse<JoinVoiceChannelResponse>.Fail(routeValidationError).ToHttpResult();

        if (routeRequest.ChannelId is not string channelId
            || !GuildChannelId.TryParse(channelId, out var parsedChannelId)
            || parsedChannelId is null)
        {
            return ApplicationResponse<JoinVoiceChannelResponse>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Route validation succeeded but channel ID parsing failed.").ToHttpResult();
        }

        if (!httpContext.TryGetAuthenticatedUserId(out var currentUserId) || currentUserId is null)
        {
            return ApplicationResponse<JoinVoiceChannelResponse>.Fail(
                    ApplicationErrorCodes.Auth.InvalidCredentials,
                    "Authenticated user identifier is missing.")
                .ToHttpResult();
        }

        var response = await handler.HandleAsync(parsedChannelId, currentUserId, cancellationToken);
        return response.ToHttpResult();
    }
}
