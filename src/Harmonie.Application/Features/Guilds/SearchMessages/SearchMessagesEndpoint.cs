using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Domain.ValueObjects.Guilds;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Guilds.SearchMessages;

public static class SearchMessagesEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/guilds/{guildId}/messages/search", HandleAsync)
            .WithName("SearchGuildMessages")
            .WithTags("Guilds")
            .RequireAuthorization()
            .WithSummary("Search messages in a guild")
            .WithDescription("Returns guild text messages matching a full-text query with optional filters and cursor pagination.")
            .Produces<SearchMessagesResponse>(StatusCodes.Status200OK)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.Guild.NotFound,
                ApplicationErrorCodes.Guild.AccessDenied,
                ApplicationErrorCodes.Channel.NotFound,
                ApplicationErrorCodes.Channel.NotText,
                ApplicationErrorCodes.Channel.AccessDenied);
    }

    private static async Task<IResult> HandleAsync(
        GuildId guildId,
        [AsParameters] SearchMessagesRequest request,
        [FromServices] IAuthenticatedHandler<SearchMessagesInput, SearchMessagesResponse> handler,
        [FromServices] IValidator<SearchMessagesRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<SearchMessagesResponse>.Fail(validationError).ToHttpResult();

        var currentUserId = httpContext.GetRequiredAuthenticatedUserId();

        var response = await handler.HandleAsync(new SearchMessagesInput(guildId, request), currentUserId, cancellationToken);
        return response.ToHttpResult();
    }
}
