extern alias Workers;

using System.Diagnostics;
using System.Net;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Interfaces.Notifications;
using Harmonie.Application.Services.Notifications;
using Harmonie.Infrastructure.Persistence.Common;
using Workers::Harmonie.Workers.Workers.Notifications;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Harmonie.API.IntegrationTests.Notifications;

public sealed class PushNotificationDispatchIntegrationTests : IClassFixture<HarmonieWebApplicationFactory>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly RecordingNotificationDeliveryAdapter _recordingAdapter;

    public PushNotificationDispatchIntegrationTests(HarmonieWebApplicationFactory factory)
    {
        _recordingAdapter = new RecordingNotificationDeliveryAdapter();
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.Configure<PushNotificationOptions>(options =>
                {
                    options.BatchSize = 500;
                    options.MaxConcurrentJobs = 2;
                    options.LockDurationSeconds = 60;
                    options.MaxAttempts = 3;
                    options.RetryBaseDelaySeconds = 1;
                });
                services.AddSingleton(_recordingAdapter);
                services.AddScoped<INotificationDeliveryAdapter>(sp => sp.GetRequiredService<RecordingNotificationDeliveryAdapter>());
                services.AddScoped<IPushNotificationBatchProcessor, PushNotificationBatchProcessor>();
            });
        });
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task WorkerBatch_WhenConversationMessageIsSent_ShouldDispatchWebPushToRecipientDeviceOnly()
    {
        var stopwatch = Stopwatch.StartNew();
        var sender = await AuthTestHelper.RegisterAsync(_client);
        var recipient = await AuthTestHelper.RegisterAsync(_client);
        var recipientEndpoint = $"https://push.example/subscriptions/{Guid.NewGuid():N}";
        var senderEndpoint = $"https://push.example/subscriptions/{Guid.NewGuid():N}";
        await RegisterWebPushDeviceAsync(recipient.AccessToken, recipientEndpoint);
        await RegisterWebPushDeviceAsync(sender.AccessToken, senderEndpoint);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, sender.AccessToken, recipient.UserId);
        var message = await ConversationTestHelper.SendConversationMessageAsync(
            _client,
            conversationId,
            "this content must not be part of the push payload",
            sender.AccessToken);

        await ProcessNotificationBatchAsync();

        stopwatch.Stop();
        Console.WriteLine($"Push notification dispatch integration test elapsed: {stopwatch.ElapsedMilliseconds}ms");

        var delivery = _recordingAdapter.Deliveries.Should().ContainSingle().Subject;
        delivery.Device.Token.Should().Be(recipientEndpoint);
        delivery.Device.UserId.Value.Should().Be(recipient.UserId);
        delivery.Payload.Type.Should().Be(NotificationDeliveryPayloadTypes.MessageCreated);
        delivery.Payload.Data.Should().BeOfType<MessageCreatedConversationNotificationData>()
            .Which.Should().Be(new MessageCreatedConversationNotificationData(
                NotificationMessageScopes.Conversation,
                message.MessageId,
                sender.UserId,
                sender.Username,
                conversationId,
                null));
        delivery.Payload.Should().NotBeEquivalentTo(new { Body = "this content must not be part of the push payload" });

        var storedStatus = await GetOutboxStatusAsync(message.MessageId);
        storedStatus.Should().Be(MessageNotificationOutboxStatuses.Processed);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private async Task RegisterWebPushDeviceAsync(string accessToken, string endpoint)
    {
        var response = await _client.SendAuthorizedPutAsync(
            "/api/notifications/push-subscriptions",
            new
            {
                endpoint,
                expirationTime = (long?)null,
                keys = new
                {
                    p256dh = "p256dh-key",
                    auth = "auth-secret"
                }
            },
            accessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private async Task ProcessNotificationBatchAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<IPushNotificationBatchProcessor>();
        await processor.ProcessBatchAsync(TestContext.Current.CancellationToken);
    }

    private async Task<string?> GetOutboxStatusAsync(Guid messageId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbSession = scope.ServiceProvider.GetRequiredService<DbSession>();
        var connection = await dbSession.GetOpenConnectionAsync(TestContext.Current.CancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT status
                              FROM message_notification_outbox
                              WHERE message_id = @MessageId
                              """;
        command.Parameters.AddWithValue("MessageId", messageId);

        var result = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        return result as string;
    }

    private sealed class RecordingNotificationDeliveryAdapter : INotificationDeliveryAdapter
    {
        private readonly List<RecordedDelivery> _deliveries = new();
        private readonly Lock _lock = new();

        public string Platform => NotificationDevicePlatforms.WebPush;

        public IReadOnlyList<RecordedDelivery> Deliveries
        {
            get
            {
                lock (_lock)
                {
                    return _deliveries.ToArray();
                }
            }
        }

        public Task<IReadOnlyList<NotificationDeliveryResult>> SendAsync(
            NotificationDeliveryPayload payload,
            IReadOnlyList<NotificationDevice> devices,
            CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                _deliveries.AddRange(devices.Select(device => new RecordedDelivery(payload, device)));
            }

            return Task.FromResult<IReadOnlyList<NotificationDeliveryResult>>(
                devices.Select(device => new NotificationDeliveryResult(device.Id, NotificationDeliveryResultStatus.Succeeded)).ToArray());
        }
    }

    private sealed record RecordedDelivery(NotificationDeliveryPayload Payload, NotificationDevice Device);
}
