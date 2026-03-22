using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Users;
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
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Guild.NotFound,
                ApplicationErrorCodes.Guild.AccessDenied,
                ApplicationErrorCodes.Guild.MemberNotFound,
                ApplicationErrorCodes.Guild.OwnerTransferToSelf);
    }

    private static async Task<IResult> HandleAsync(
        GuildId guildId,
        [FromBody] TransferOwnershipRequest request,
        [FromServices] IAuthenticatedHandler<TransferOwnershipInput, bool> handler,
        [FromServices] IValidator<TransferOwnershipRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<bool>.Fail(validationError).ToHttpResult();

        if (request.NewOwnerId is not string newOwnerIdStr
            || !UserId.TryParse(newOwnerIdStr, out var parsedNewOwnerId)
            || parsedNewOwnerId is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Body validation succeeded but new owner ID parsing failed.").ToHttpResult();
        }

        var callerId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(new TransferOwnershipInput(guildId, parsedNewOwnerId), callerId, cancellationToken);

        if (response.Success)
            return Results.NoContent();

        return response.ToHttpResult();
    }
}
