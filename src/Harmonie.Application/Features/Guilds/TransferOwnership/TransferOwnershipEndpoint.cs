using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Guilds.TransferOwnership;

public static class TransferOwnershipEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/guilds/{guildId}/owner/transfer", HandleAsync)
            .WithName("TransferOwnership")
            .WithTags("Guilds")
            .RequireAuthorization()
            .WithSummary("Transfer guild ownership")
            .WithDescription("Transfers ownership of the guild to an existing member. Only the current owner can perform this action.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesErrors(
                ApplicationErrorCodes.Guild.NotFound,
                ApplicationErrorCodes.Guild.AccessDenied,
                ApplicationErrorCodes.Guild.MemberNotFound,
                ApplicationErrorCodes.Guild.OwnerTransferToSelf);
    }

    private static async Task<IResult> HandleAsync(
        [AsParameters] TransferOwnershipRouteRequest routeRequest,
        [FromBody] TransferOwnershipRequest request,
        [FromServices] TransferOwnershipHandler handler,
        [FromServices] IValidator<TransferOwnershipRouteRequest> routeValidator,
        [FromServices] IValidator<TransferOwnershipRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var routeValidationError = await routeRequest.ValidateAsync(routeValidator, cancellationToken);
        if (routeValidationError is not null)
            return ApplicationResponse<bool>.Fail(routeValidationError).ToHttpResult();

        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<bool>.Fail(validationError).ToHttpResult();

        if (routeRequest.GuildId is not string guildIdStr
            || !GuildId.TryParse(guildIdStr, out var parsedGuildId)
            || parsedGuildId is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Route validation succeeded but guild ID parsing failed.").ToHttpResult();
        }

        if (request.NewOwnerId is not string newOwnerIdStr
            || !UserId.TryParse(newOwnerIdStr, out var parsedNewOwnerId)
            || parsedNewOwnerId is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Body validation succeeded but new owner ID parsing failed.").ToHttpResult();
        }

        if (!httpContext.TryGetAuthenticatedUserId(out var callerId) || callerId is null)
        {
            return ApplicationResponse<bool>.Fail(
                    ApplicationErrorCodes.Auth.InvalidCredentials,
                    "Authenticated user identifier is missing.")
                .ToHttpResult();
        }

        var response = await handler.HandleAsync(parsedGuildId, callerId, parsedNewOwnerId, cancellationToken);

        if (response.Success)
            return Results.NoContent();

        return response.ToHttpResult();
    }
}
