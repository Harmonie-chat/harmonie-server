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
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Common.DomainRuleViolation);
    }

    private static async Task<IResult> HandleAsync(
        [FromBody] CreateGuildRequest request,
        [FromServices] IAuthenticatedHandler<CreateGuildRequest, CreateGuildResponse> handler,
        [FromServices] IValidator<CreateGuildRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<CreateGuildResponse>.Fail(validationError).ToHttpResult(httpContext);

        var currentUserId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(request, currentUserId, cancellationToken);
        return response.ToCreatedHttpResult(data => $"/api/guilds/{data.GuildId}", httpContext);
    }
}
