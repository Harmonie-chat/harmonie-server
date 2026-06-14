using System.Text.Json;
using Harmonie.Application.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.OpenApi;

namespace Harmonie.Application.Features.Notifications.GetWebPushPublicKey;

public static class GetWebPushPublicKeyEndpoint
{
    private static readonly JsonSerializerOptions OpenApiExampleSerializerOptions = new(JsonSerializerDefaults.Web);

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/notifications/web-push-public-key", HandleAsync)
            .WithName("GetWebPushPublicKey")
            .WithTags("Notifications")
            .AllowAnonymous()
            .WithSummary("Get the Web Push VAPID public key")
            .WithDescription("Returns the public VAPID key used by clients to create browser Web Push subscriptions. The private VAPID key is never exposed.")
            .Produces<GetWebPushPublicKeyResponse>(StatusCodes.Status200OK)
            .ProducesErrors(ApplicationErrorCodes.Notification.WebPushNotConfigured)
            .AddOpenApiOperationTransformer((operation, _, _) =>
            {
                if (operation?.Responses is not null
                    && operation.Responses.TryGetValue("200", out var response)
                    && response.Content is not null
                    && response.Content.TryGetValue("application/json", out var mediaType))
                {
                    mediaType.Example = JsonSerializer.SerializeToNode(
                        new GetWebPushPublicKeyResponse("BDp4Base64UrlVapidPublicKeyExample"),
                        OpenApiExampleSerializerOptions);
                }

                return Task.CompletedTask;
            });
    }

    private static async Task<IResult> HandleAsync(
        [FromServices] IHandler<Unit, GetWebPushPublicKeyResponse> handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var response = await handler.HandleAsync(Unit.Value, cancellationToken);
        return response.ToHttpResult(httpContext);
    }
}
