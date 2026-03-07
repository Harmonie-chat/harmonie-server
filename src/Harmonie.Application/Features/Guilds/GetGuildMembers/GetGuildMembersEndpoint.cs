using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Guilds.GetGuildMembers;

public static class GetGuildMembersEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/guilds/{guildId}/members", HandleAsync)
            .WithName("GetGuildMembers")
            .WithTags("Guilds")
            .RequireAuthorization()
            .WithSummary("List guild members")
            .WithDescription("Returns guild members for an authenticated guild member.")
            .Produces<GetGuildMembersResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesErrors(
                ApplicationErrorCodes.Guild.NotFound,
                ApplicationErrorCodes.Guild.AccessDenied);
    }

    private static async Task<IResult> HandleAsync(
        [AsParameters] GetGuildMembersRequest request,
        [FromServices] GetGuildMembersHandler handler,
        [FromServices] IValidator<GetGuildMembersRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<GetGuildMembersResponse>.Fail(validationError).ToHttpResult();

        if (request.GuildId is not string guildId
            || !GuildId.TryParse(guildId, out var parsedGuildId)
            || parsedGuildId is null)
        {
            return ApplicationResponse<GetGuildMembersResponse>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Route validation succeeded but guild ID parsing failed.").ToHttpResult();
        }

        if (!httpContext.TryGetAuthenticatedUserId(out var currentUserId) || currentUserId is null)
        {
            return ApplicationResponse<GetGuildMembersResponse>.Fail(
                    ApplicationErrorCodes.Auth.InvalidCredentials,
                    "Authenticated user identifier is missing.")
                .ToHttpResult();
        }

        var response = await handler.HandleAsync(parsedGuildId, currentUserId, cancellationToken);
        return response.ToHttpResult();
    }
}
