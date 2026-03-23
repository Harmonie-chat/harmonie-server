using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects.Guilds;
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
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Common.DomainRuleViolation,
                ApplicationErrorCodes.Guild.NotFound,
                ApplicationErrorCodes.Guild.AccessDenied,
                ApplicationErrorCodes.Channel.NameConflict);
    }

    private static async Task<IResult> HandleAsync(
        GuildId guildId,
        [FromBody] CreateChannelRequest request,
        [FromServices] IAuthenticatedHandler<CreateChannelInput, CreateChannelResponse> handler,
        [FromServices] IValidator<CreateChannelRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<CreateChannelResponse>.Fail(validationError).ToHttpResult();

        var callerId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(
            new CreateChannelInput(guildId, request.Name, request.Type.ToDomain(), request.Position),
            callerId,
            cancellationToken);

        return response.ToCreatedHttpResult(data => $"/api/guilds/{data.GuildId}/channels/{data.ChannelId}");
    }
}
