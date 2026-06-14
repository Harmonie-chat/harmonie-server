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
        builder.AppendLine("Documentation-only Development endpoint. It documents outbound push payload shapes emitted by Harmonie.Workers; it does not send notifications.");
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
