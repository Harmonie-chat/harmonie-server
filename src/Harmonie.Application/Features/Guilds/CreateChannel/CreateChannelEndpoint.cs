using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Guilds.CreateChannel;

public static class CreateChannelEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/guilds/{guildId}/channels", HandleAsync)
            .WithName("CreateChannel")
            .WithTags("Guilds")
            .RequireAuthorization()
            .WithSummary("Create a guild channel")
            .WithDescription("Creates a new text or voice channel in the guild. Only guild admins can create channels.")
            .Produces<CreateChannelResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Common.DomainRuleViolation,
                ApplicationErrorCodes.Guild.NotFound,
                ApplicationErrorCodes.Guild.AccessDenied,
                ApplicationErrorCodes.Channel.NameConflict);
    }

    private static async Task<IResult> HandleAsync(
        [AsParameters] CreateChannelRouteRequest routeRequest,
        [FromBody] CreateChannelRequest request,
        [FromServices] CreateChannelHandler handler,
        [FromServices] IValidator<CreateChannelRouteRequest> routeValidator,
        [FromServices] IValidator<CreateChannelRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var routeValidationError = await routeRequest.ValidateAsync(routeValidator, cancellationToken);
        if (routeValidationError is not null)
            return ApplicationResponse<CreateChannelResponse>.Fail(routeValidationError).ToHttpResult();

        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<CreateChannelResponse>.Fail(validationError).ToHttpResult();

        if (routeRequest.GuildId is not string guildIdStr
            || !GuildId.TryParse(guildIdStr, out var parsedGuildId)
            || parsedGuildId is null)
        {
            return ApplicationResponse<CreateChannelResponse>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Route validation succeeded but guild ID parsing failed.").ToHttpResult();
        }

        if (!httpContext.TryGetAuthenticatedUserId(out var callerId) || callerId is null)
        {
            return ApplicationResponse<CreateChannelResponse>.Fail(
                    ApplicationErrorCodes.Auth.InvalidCredentials,
                    "Authenticated user identifier is missing.")
                .ToHttpResult();
        }

        var response = await handler.HandleAsync(
            parsedGuildId,
            callerId,
            request.Name,
            request.Type.ToDomain(),
            request.Position,
            cancellationToken);

        return response.ToCreatedHttpResult(data => $"/api/guilds/{data.GuildId}/channels/{data.ChannelId}");
    }
}
