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
            .Accepts<UpdateChannelOpenApiRequest>("application/json")
            .Produces<UpdateChannelResponse>(StatusCodes.Status200OK)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Common.DomainRuleViolation,
                ApplicationErrorCodes.Guild.AccessDenied,
                ApplicationErrorCodes.Channel.NotFound,
                ApplicationErrorCodes.Channel.AccessDenied,
                ApplicationErrorCodes.Channel.NameConflict);
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

    internal sealed record UpdateChannelOpenApiRequest(
        string? Name,
        int? Position);
}
