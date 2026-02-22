using FluentValidation;
using Harmonie.Application.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Guilds.CreateGuild;

public static class CreateGuildEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/guilds", HandleAsync)
            .WithName("CreateGuild")
            .WithTags("Guilds")
            .RequireAuthorization()
            .WithSummary("Create a guild")
            .WithDescription("Creates a guild, owner admin membership, and default channels.")
            .Produces<CreateGuildResponse>(StatusCodes.Status201Created)
            .Produces<ApplicationError>(StatusCodes.Status400BadRequest)
            .Produces<ApplicationError>(StatusCodes.Status401Unauthorized)
            .Produces<ApplicationError>(StatusCodes.Status500InternalServerError);
    }

    private static async Task<IResult> HandleAsync(
        [FromBody] CreateGuildRequest request,
        [FromServices] CreateGuildHandler handler,
        [FromServices] IValidator<CreateGuildRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<CreateGuildResponse>.Fail(validationError).ToHttpResult();

        if (!httpContext.TryGetAuthenticatedUserId(out var currentUserId) || currentUserId is null)
        {
            return ApplicationResponse<CreateGuildResponse>.Fail(
                ApplicationErrorCodes.Auth.InvalidCredentials,
                "Authenticated user identifier is missing.")
                .ToHttpResult();
        }

        var response = await handler.HandleAsync(request, currentUserId, cancellationToken);
        return response.ToCreatedHttpResult(data => $"/api/guilds/{data.GuildId}");
    }
}
