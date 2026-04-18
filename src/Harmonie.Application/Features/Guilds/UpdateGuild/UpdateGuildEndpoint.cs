using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects.Guilds;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Guilds.UpdateGuild;

public static class UpdateGuildEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/guilds/{guildId}", HandleAsync)
            .WithName("UpdateGuild")
            .WithTags("Guilds")
            .RequireAuthorization()
            .WithSummary("Update guild settings")
            .WithDescription("Updates guild name and icon settings. Only the guild owner or an admin can update guild settings.")
            .WithJsonRequestBodyDocumentation(
                "Partial guild update. Omit a field to keep its current value. Send `icon` as null to clear icon appearance. `name` cannot be null.",
                typeof(UpdateGuildOpenApiRequest),
                (
                    "updateIconAppearance",
                    "Update icon appearance only",
                    new
                    {
                        icon = new { color = "#7C3AED", name = "sword", bg = "#1F2937" }
                    }),
                (
                    "updateNameAndIconFile",
                    "Update name and icon file",
                    new
                    {
                        name = "My Guild",
                        iconFileId = "8d7205f2-2d62-49a5-8873-b1d331ed7e8c"
                    }),
                (
                    "clearIcon",
                    "Clear icon file and generated icon",
                    new
                    {
                        iconFileId = (string?)null,
                        icon = (object?)null
                    }))
            .Produces<UpdateGuildResponse>(StatusCodes.Status200OK)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Guild.NotFound,
                ApplicationErrorCodes.Guild.AccessDenied);
    }

    private static async Task<IResult> HandleAsync(
        GuildId guildId,
        [FromBody] UpdateGuildRequest request,
        [FromServices] IAuthenticatedHandler<UpdateGuildInput, UpdateGuildResponse> handler,
        [FromServices] IValidator<UpdateGuildRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<UpdateGuildResponse>.Fail(validationError).ToHttpResult(httpContext);

        var callerId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(
            new UpdateGuildInput(
                guildId,
                request.Name,
                request.IconFileId,
                request.IconColor,
                request.IconName,
                request.IconBg,
                request.NameIsSet,
                request.IconFileIdIsSet,
                request.IconColorIsSet,
                request.IconNameIsSet,
                request.IconBgIsSet),
            callerId,
            cancellationToken);
        return response.ToHttpResult(httpContext);
    }

    internal sealed record UpdateGuildOpenApiRequest(
        string? Name,
        string? IconFileId,
        UpdateGuildOpenApiIconRequest? Icon);

    internal sealed record UpdateGuildOpenApiIconRequest(
        string? Color,
        string? Name,
        string? Bg);
}
