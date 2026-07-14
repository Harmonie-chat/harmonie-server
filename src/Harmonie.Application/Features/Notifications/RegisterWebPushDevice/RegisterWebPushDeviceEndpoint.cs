using FluentValidation;
using Harmonie.Application.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Harmonie.Application.Features.Notifications.RegisterWebPushDevice;

public static class RegisterWebPushDeviceEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/notifications/push-subscriptions", HandleAsync)
            .WithName("RegisterWebPushDevice")
            .WithTags("Notifications")
            .RequireAuthorization()
            .WithSummary("Register a Web Push notification device")
            .WithDescription("Registers or updates the authenticated user's Web Push subscription. Internally this is stored as a web_push notification device so other push platforms can be added later. Delivery is asynchronous through the worker host; the backend currently emits minimal message.created business payloads for conversation and guild channel messages.")
            .WithJsonRequestBodyDocumentation(
                "Web Push subscription returned by PushManager.subscribe().",
                (
                    "webPushSubscription",
                    "Register a browser Web Push subscription",
                    new
                    {
                        endpoint = "https://updates.push.services.mozilla.com/wpush/v2/example",
                        expirationTime = (long?)null,
                        keys = new
                        {
                            p256dh = "base64url-p256dh-key",
                            auth = "base64url-auth-secret"
                        }
                    }))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesErrors(
                ApplicationErrorCodes.Common.ValidationFailed,
                ApplicationErrorCodes.Auth.InvalidCredentials,
                ApplicationErrorCodes.User.NotFound);
    }

    private static async Task<IResult> HandleAsync(
        [FromBody] RegisterWebPushDeviceRequest request,
        [FromServices] IAuthenticatedHandler<RegisterWebPushDeviceRequest, bool> handler,
        [FromServices] IValidator<RegisterWebPushDeviceRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationError = await request.ValidateAsync(validator, cancellationToken);
        if (validationError is not null)
            return ApplicationResponse<bool>.Fail(validationError).ToHttpResult(httpContext);

        var currentUserId = httpContext.GetRequiredAuthenticatedUserId();
        var response = await handler.HandleAsync(request, currentUserId, cancellationToken);

        if (!response.Success)
            return response.ToHttpResult(httpContext);

        return Results.NoContent();
    }
}
