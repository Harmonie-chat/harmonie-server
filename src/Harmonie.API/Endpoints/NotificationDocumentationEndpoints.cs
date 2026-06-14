using System.Text;
using Harmonie.Application.Interfaces.Notifications;
using Harmonie.Application.Services.Notifications;

namespace Harmonie.API.Endpoints;

public static class NotificationDocumentationEndpoints
{
    public static void MapNotificationDocumentationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/docs/notifications/push-payloads", Results.NoContent)
            .WithName("GetPushNotificationPayloadContracts")
            .WithTags("Notifications")
            .WithSummary("Describe outbound push notification payload contracts")
            .WithDescription(BuildDescription())
            .Produces(StatusCodes.Status204NoContent);
    }

    private static string BuildDescription()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Documentation-only Development endpoint. It documents push payloads emitted by Harmonie.Workers; it does not send notifications.");
        builder.AppendLine();
        builder.AppendLine("## Client delivery flow");
        builder.AppendLine();
        builder.AppendLine("1. The client registers a browser Web Push subscription with `PUT /api/notifications/push-subscriptions`.");
        builder.AppendLine("2. Harmonie.Workers sends encrypted payloads to the browser push service endpoint from that subscription.");
        builder.AppendLine("3. The browser wakes the frontend service worker and raises a `push` event.");
        builder.AppendLine("4. The service worker reads `event.data.json()`, renders the visible notification, and handles `notificationclick` for routing.");
        builder.AppendLine();
        builder.AppendLine("No client `GET`/`PUT` receives the push itself. The app may optionally call the API after a push or click to fetch fresh message/channel/conversation details.");
        builder.AppendLine();
        builder.AppendLine("## Payload notes");
        builder.AppendLine();
        builder.AppendLine("- Payloads intentionally exclude message content.");
        builder.AppendLine("- Frontend/service worker owns notification text, routing, i18n, icon, badge, and tag.");
        builder.AppendLine("- `type` identifies the event; `data.scope` narrows the message target shape.");
        builder.AppendLine();
        builder.AppendLine("## `message.created` channel payload");
        builder.AppendLine();
        builder.AppendLine("```json");
        builder.AppendLine("{");
        builder.AppendLine($"  \"type\": \"{NotificationDeliveryPayloadTypes.MessageCreated}\",");
        builder.AppendLine("  \"data\": {");
        builder.AppendLine($"    \"scope\": \"{NotificationMessageScopes.Channel}\",");
        builder.AppendLine("    \"messageId\": \"11111111-1111-1111-1111-111111111111\",");
        builder.AppendLine("    \"authorUserId\": \"22222222-2222-2222-2222-222222222222\",");
        builder.AppendLine("    \"authorDisplayName\": \"alice\",");
        builder.AppendLine("    \"guildId\": \"33333333-3333-3333-3333-333333333333\",");
        builder.AppendLine("    \"guildName\": \"Harmonie\",");
        builder.AppendLine("    \"channelId\": \"44444444-4444-4444-4444-444444444444\",");
        builder.AppendLine("    \"channelName\": \"general\"");
        builder.AppendLine("  }");
        builder.AppendLine("}");
        builder.AppendLine("```");
        builder.AppendLine();
        builder.AppendLine("## `message.created` conversation payload");
        builder.AppendLine();
        builder.AppendLine("```json");
        builder.AppendLine("{");
        builder.AppendLine($"  \"type\": \"{NotificationDeliveryPayloadTypes.MessageCreated}\",");
        builder.AppendLine("  \"data\": {");
        builder.AppendLine($"    \"scope\": \"{NotificationMessageScopes.Conversation}\",");
        builder.AppendLine("    \"messageId\": \"55555555-5555-5555-5555-555555555555\",");
        builder.AppendLine("    \"authorUserId\": \"22222222-2222-2222-2222-222222222222\",");
        builder.AppendLine("    \"authorDisplayName\": \"alice\",");
        builder.AppendLine("    \"conversationId\": \"66666666-6666-6666-6666-666666666666\",");
        builder.AppendLine("    \"conversationName\": null");
        builder.AppendLine("  }");
        builder.AppendLine("}");
        builder.AppendLine("```");

        return builder.ToString();
    }
}
