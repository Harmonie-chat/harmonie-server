using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Channels.DeleteChannel;

public static class DeleteChannelEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/channels/{channelId}", HandleAsync)
            .WithName("DeleteChannel")
            .WithTags("Channels")
            .RequireAuthorization()
            .WithSummary("Delete a guild channel")
            .WithDescription("Deletes a guild channel. Only guild admins can delete channels. The default channel cannot be deleted.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Guild.AccessDenied,
                ApplicationErrorCodes.Channel.NotFound,
                ApplicationErrorCodes.Channel.AccessDenied,
                ApplicationErrorCodes.Channel.CannotDeleteDefault);
    }

    private static async Task<IResult> HandleAsync(
        [AsParameters] DeleteChannelRouteRequest routeRequest,
        [FromServices] DeleteChannelHandler handler,
        [FromServices] IValidator<DeleteChannelRouteRequest> routeValidator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var routeValidationError = await routeRequest.ValidateAsync(routeValidator, cancellationToken);
        if (routeValidationError is not null)
            return ApplicationResponse<bool>.Fail(routeValidationError).ToHttpResult();

        if (routeRequest.ChannelId is not string channelIdStr
            || !GuildChannelId.TryParse(channelIdStr, out var parsedChannelId)
            || parsedChannelId is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Route validation succeeded but channel ID parsing failed.").ToHttpResult();
        }

        if (!httpContext.TryGetAuthenticatedUserId(out var callerId) || callerId is null)
        {
            return ApplicationResponse<bool>.Fail(
                    ApplicationErrorCodes.Auth.InvalidCredentials,
                    "Authenticated user identifier is missing.")
                .ToHttpResult();
        }

        var response = await handler.HandleAsync(parsedChannelId, callerId, cancellationToken);

        if (response.Success)
            return Results.NoContent();

        return response.ToHttpResult();
    }
}
