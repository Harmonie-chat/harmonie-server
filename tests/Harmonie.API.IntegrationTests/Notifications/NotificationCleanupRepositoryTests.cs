using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Interfaces.Notifications;
using Harmonie.Infrastructure.Persistence.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Harmonie.API.IntegrationTests.Notifications;

public sealed class NotificationCleanupRepositoryTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HarmonieWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public NotificationCleanupRepositoryTests(HarmonieWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task DeleteOutboxBatchAsync_ShouldRespectRetentionCutoffAndBatchSize()
    {
        var user = await AuthTestHelper.RegisterAsync(_client);
        var (_, channelId) = await ChannelTestHelper.CreateGuildAndChannelAsync(_client, user.AccessToken);
        var firstOldProcessedMessage = await ChannelTestHelper.SendChannelMessageAsync(_client, channelId, "first old processed cleanup", user.AccessToken);
        var secondOldProcessedMessage = await ChannelTestHelper.SendChannelMessageAsync(_client, channelId, "second old processed cleanup", user.AccessToken);
        var recentProcessedMessage = await ChannelTestHelper.SendChannelMessageAsync(_client, channelId, "recent processed cleanup", user.AccessToken);
        var oldFailedMessage = await ChannelTestHelper.SendChannelMessageAsync(_client, channelId, "old failed cleanup", user.AccessToken);
        var cutoffUtc = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await SetOutboxStatusAsync(firstOldProcessedMessage.MessageId, MessageNotificationOutboxStatuses.Processed, cutoffUtc.AddDays(-2));
        await SetOutboxStatusAsync(secondOldProcessedMessage.MessageId, MessageNotificationOutboxStatuses.Processed, cutoffUtc.AddDays(-1));
        await SetOutboxStatusAsync(recentProcessedMessage.MessageId, MessageNotificationOutboxStatuses.Processed, cutoffUtc.AddDays(1));
        await SetOutboxStatusAsync(oldFailedMessage.MessageId, MessageNotificationOutboxStatuses.Failed, cutoffUtc.AddDays(-1));

        var firstProcessedBatch = await DeleteOutboxBatchAsync(
            MessageNotificationOutboxStatuses.Processed,
            cutoffUtc,
            batchSize: 1);
        var secondProcessedBatch = await DeleteOutboxBatchAsync(
            MessageNotificationOutboxStatuses.Processed,
            cutoffUtc,
            batchSize: 1);
        var thirdProcessedBatch = await DeleteOutboxBatchAsync(
            MessageNotificationOutboxStatuses.Processed,
            cutoffUtc,
            batchSize: 1);
        var failedBatch = await DeleteOutboxBatchAsync(
            MessageNotificationOutboxStatuses.Failed,
            cutoffUtc,
            batchSize: 1);

        firstProcessedBatch.Should().Be(1);
        secondProcessedBatch.Should().Be(1);
        thirdProcessedBatch.Should().Be(0);
        failedBatch.Should().Be(1);
        (await OutboxJobExistsAsync(firstOldProcessedMessage.MessageId)).Should().BeFalse();
        (await OutboxJobExistsAsync(secondOldProcessedMessage.MessageId)).Should().BeFalse();
        (await OutboxJobExistsAsync(oldFailedMessage.MessageId)).Should().BeFalse();
        (await OutboxJobExistsAsync(recentProcessedMessage.MessageId)).Should().BeTrue();
    }

    [Fact]
    public async Task DeleteExpiredDevicesBatchAsync_ShouldRespectGracePeriodAndBatchSize()
    {
        var user = await AuthTestHelper.RegisterAsync(_client);
        var oldFirstDeviceId = Guid.NewGuid();
        var oldSecondDeviceId = Guid.NewGuid();
        var recentDeviceId = Guid.NewGuid();
        var cutoffUtc = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await InsertDeviceAsync(oldFirstDeviceId, user.UserId, cutoffUtc.AddDays(-2));
        await InsertDeviceAsync(oldSecondDeviceId, user.UserId, cutoffUtc.AddDays(-1));
        await InsertDeviceAsync(recentDeviceId, user.UserId, cutoffUtc.AddDays(1));

        var firstBatch = await DeleteExpiredDevicesBatchAsync(cutoffUtc, batchSize: 1);
        var secondBatch = await DeleteExpiredDevicesBatchAsync(cutoffUtc, batchSize: 1);
        var thirdBatch = await DeleteExpiredDevicesBatchAsync(cutoffUtc, batchSize: 1);

        firstBatch.Should().Be(1);
        secondBatch.Should().Be(1);
        thirdBatch.Should().Be(0);
        (await NotificationDeviceExistsAsync(oldFirstDeviceId)).Should().BeFalse();
        (await NotificationDeviceExistsAsync(oldSecondDeviceId)).Should().BeFalse();
        (await NotificationDeviceExistsAsync(recentDeviceId)).Should().BeTrue();
    }

    private async Task<int> DeleteOutboxBatchAsync(string status, DateTime processedBeforeUtc, int batchSize)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<INotificationCleanupRepository>();
        return await repository.DeleteOutboxBatchAsync(
            status,
            processedBeforeUtc,
            batchSize,
            TestContext.Current.CancellationToken);
    }

    private async Task<int> DeleteExpiredDevicesBatchAsync(DateTime expiresBeforeUtc, int batchSize)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<INotificationCleanupRepository>();
        return await repository.DeleteExpiredDevicesBatchAsync(
            expiresBeforeUtc,
            batchSize,
            TestContext.Current.CancellationToken);
    }

    private async Task SetOutboxStatusAsync(Guid messageId, string status, DateTime processedAtUtc)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbSession = scope.ServiceProvider.GetRequiredService<DbSession>();
        var connection = await dbSession.GetOpenConnectionAsync(TestContext.Current.CancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
                              UPDATE message_notification_outbox
                              SET status = @Status,
                                  processed_at_utc = @ProcessedAtUtc,
                                  locked_until_utc = NULL
                              WHERE message_id = @MessageId
                              """;
        command.Parameters.AddWithValue("Status", status);
        command.Parameters.AddWithValue("ProcessedAtUtc", processedAtUtc);
        command.Parameters.AddWithValue("MessageId", messageId);

        var rows = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        rows.Should().Be(1);
    }

    private async Task InsertDeviceAsync(Guid deviceId, Guid userId, DateTime expiresAtUtc)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbSession = scope.ServiceProvider.GetRequiredService<DbSession>();
        var connection = await dbSession.GetOpenConnectionAsync(TestContext.Current.CancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
                              INSERT INTO notification_devices (
                                  id,
                                  user_id,
                                  platform,
                                  token,
                                  web_push_p256dh,
                                  web_push_auth,
                                  expires_at_utc,
                                  created_at_utc,
                                  updated_at_utc)
                              VALUES (
                                  @Id,
                                  @UserId,
                                  'web_push',
                                  @Token,
                                  'p256dh-key',
                                  'auth-secret',
                                  @ExpiresAtUtc,
                                  @NowUtc,
                                  @NowUtc)
                              """;
        command.Parameters.AddWithValue("Id", deviceId);
        command.Parameters.AddWithValue("UserId", userId);
        command.Parameters.AddWithValue("Token", $"https://push.example/subscriptions/{deviceId:N}");
        command.Parameters.AddWithValue("ExpiresAtUtc", expiresAtUtc);
        command.Parameters.AddWithValue("NowUtc", DateTime.UtcNow);

        var rows = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        rows.Should().Be(1);
    }

    private async Task<bool> OutboxJobExistsAsync(Guid messageId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbSession = scope.ServiceProvider.GetRequiredService<DbSession>();
        var connection = await dbSession.GetOpenConnectionAsync(TestContext.Current.CancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS (SELECT 1 FROM message_notification_outbox WHERE message_id = @MessageId)";
        command.Parameters.AddWithValue("MessageId", messageId);

        var result = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        result.Should().BeOfType<bool>();
        return (bool)result;
    }

    private async Task<bool> NotificationDeviceExistsAsync(Guid deviceId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbSession = scope.ServiceProvider.GetRequiredService<DbSession>();
        var connection = await dbSession.GetOpenConnectionAsync(TestContext.Current.CancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS (SELECT 1 FROM notification_devices WHERE id = @DeviceId)";
        command.Parameters.AddWithValue("DeviceId", deviceId);

        var result = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        result.Should().BeOfType<bool>();
        return (bool)result;
    }
}
