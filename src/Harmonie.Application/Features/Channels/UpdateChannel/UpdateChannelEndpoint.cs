using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Channels.UpdateChannel;

public static class UpdateChannelEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/channels/{channelId}", HandleAsync)
            .WithName("UpdateChannel")
            .WithTags("Channels")
            .RequireAuthorization()
            .WithSummary("Update a guild channel")
            .WithDescription("Updates the name and/or position of a channel. Only guild admins can update channels.")
            .Produces<UpdateChannelResponse>(StatusCodes.Status200OK)
            .Produces<ApplicationError>(StatusCodes.Status400BadRequest)
            .Produces<ApplicationError>(StatusCodes.Status401Unauthorized)
            .Produces<ApplicationError>(StatusCodes.Status403Forbidden)
            .Produces<ApplicationError>(StatusCodes.Status404NotFound)
            .Produces<ApplicationError>(StatusCodes.Status409Conflict)
            .Produces<ApplicationError>(StatusCodes.Status500InternalServerError);
    }

    private static async Task<IResult> HandleAsync(
        [AsParameters] UpdateChannelRouteRequest routeRequest,
        [FromBody] UpdateChannelRequest request,
        [FromServices] UpdateChannelHandler handler,
        [FromServices] IValidator<UpdateChannelRouteRequest> routeValidator,
        [FromServices] IValidator<UpdateChannelRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var routeValidationError = await routeRequest.ValidateAsync(routeValidator, cancellationToken);
        if (routeValidationError is not null)
            return ApplicationResponse<UpdateChannelResponse>.Fail(routeValidationError).ToHttpResult();

        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<UpdateChannelResponse>.Fail(validationError).ToHttpResult();

        if (routeRequest.ChannelId is not string channelIdStr
            || !GuildChannelId.TryParse(channelIdStr, out var parsedChannelId)
            || parsedChannelId is null)
        {
            return ApplicationResponse<UpdateChannelResponse>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Route validation succeeded but channel ID parsing failed.").ToHttpResult();
        }

        if (!httpContext.TryGetAuthenticatedUserId(out var callerId) || callerId is null)
        {
            return ApplicationResponse<UpdateChannelResponse>.Fail(
                    ApplicationErrorCodes.Auth.InvalidCredentials,
                    "Authenticated user identifier is missing.")
                .ToHttpResult();
        }

        var response = await handler.HandleAsync(parsedChannelId, callerId, request, cancellationToken);
        return response.ToHttpResult();
    }
}
