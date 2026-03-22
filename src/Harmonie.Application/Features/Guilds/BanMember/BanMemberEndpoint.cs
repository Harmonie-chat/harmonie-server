using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Guilds.BanMember;

public static class BanMemberEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/guilds/{guildId}/bans", HandleAsync)
            .WithName("BanMember")
            .WithTags("Guilds")
            .RequireAuthorization()
            .WithSummary("Ban a member from a guild")
            .WithDescription("Bans the specified user from the guild with optional message purge. Only admins can ban. The guild owner cannot be banned. Non-owner admins cannot ban other admins.")
            .Produces<BanMemberResponse>(StatusCodes.Status201Created)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Guild.NotFound,
                ApplicationErrorCodes.Guild.AccessDenied,
                ApplicationErrorCodes.Guild.CannotBanSelf,
                ApplicationErrorCodes.Guild.OwnerCannotBeBanned,
                ApplicationErrorCodes.Guild.AlreadyBanned,
                ApplicationErrorCodes.Common.DomainRuleViolation);
    }

    private static async Task<IResult> HandleAsync(
        GuildId guildId,
        [FromBody] BanMemberRequest request,
        [FromServices] BanMemberHandler handler,
        [FromServices] IValidator<BanMemberRequest> bodyValidator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var bodyValidationError = await request.ValidateAsync(bodyValidator, cancellationToken);
        if (bodyValidationError is not null)
            return ApplicationResponse<BanMemberResponse>.Fail(bodyValidationError).ToHttpResult();

        if (!UserId.TryParse(request.UserId, out var parsedTargetId)
            || parsedTargetId is null)
        {
            return ApplicationResponse<BanMemberResponse>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Body validation succeeded but user ID parsing failed.").ToHttpResult();
        }

        var callerId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(
            guildId,
            callerId,
            parsedTargetId,
            request.Reason,
            request.PurgeMessagesDays,
            cancellationToken);

        return response.ToCreatedHttpResult(data => $"/api/guilds/{data.GuildId}/bans");
    }
}
