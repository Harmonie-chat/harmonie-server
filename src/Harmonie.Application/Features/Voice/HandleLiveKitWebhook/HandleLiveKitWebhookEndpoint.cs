using FluentValidation;
using Harmonie.Application.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Voice.HandleLiveKitWebhook;

public static class HandleLiveKitWebhookEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/webhooks/livekit", HandleAsync)
            .WithName("HandleLiveKitWebhook")
            .WithTags("Webhooks")
            .WithSummary("Process a LiveKit webhook")
            .WithDescription("Validates a signed LiveKit webhook and broadcasts voice presence updates to guild members.")
            .Accepts<string>("application/webhook+json")
            .Produces<HandleLiveKitWebhookResponse>(StatusCodes.Status200OK)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials);
    }

    private static async Task<IResult> HandleAsync(
        HttpRequest httpRequest,
        [FromHeader(Name = "Authorization")] string? authorizationHeader,
        [FromServices] IHandler<HandleLiveKitWebhookRequest, HandleLiveKitWebhookResponse> handler,
        [FromServices] IValidator<HandleLiveKitWebhookRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(httpRequest.Body);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);

        var request = new HandleLiveKitWebhookRequest(rawBody, authorizationHeader);
        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<HandleLiveKitWebhookResponse>.Fail(validationError).ToHttpResult(httpContext);

        var response = await handler.HandleAsync(request, cancellationToken);
        return response.ToHttpResult(httpContext);
    }
}
