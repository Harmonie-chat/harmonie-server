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
        var message = await ChannelTestHelper.SendChannelMessageAsync(
            _client,
            channelId,
            "notification cleanup repository",
            user.AccessToken);
        var firstOldProcessedJobId = Guid.NewGuid();
        var secondOldProcessedJobId = Guid.NewGuid();
        var cutoffProcessedJobId = Guid.NewGuid();
        var recentProcessedJobId = Guid.NewGuid();
        var oldFailedJobId = Guid.NewGuid();
        var cutoffUtc = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await DeleteOutboxUntilEmptyAsync(MessageNotificationOutboxStatuses.Processed, cutoffUtc);
        await DeleteOutboxUntilEmptyAsync(MessageNotificationOutboxStatuses.Failed, cutoffUtc);
        await ReplaceOutboxWithCleanupJobsAsync(
            message.MessageId,
            [
                (firstOldProcessedJobId, MessageNotificationOutboxStatuses.Processed, cutoffUtc.AddDays(-2)),
                (secondOldProcessedJobId, MessageNotificationOutboxStatuses.Processed, cutoffUtc.AddDays(-1)),
                (cutoffProcessedJobId, MessageNotificationOutboxStatuses.Processed, cutoffUtc),
                (recentProcessedJobId, MessageNotificationOutboxStatuses.Processed, cutoffUtc.AddDays(1)),
                (oldFailedJobId, MessageNotificationOutboxStatuses.Failed, cutoffUtc.AddDays(-1))
            ]);

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
        (await OutboxJobExistsAsync(firstOldProcessedJobId)).Should().BeFalse();
        (await OutboxJobExistsAsync(secondOldProcessedJobId)).Should().BeFalse();
        (await OutboxJobExistsAsync(oldFailedJobId)).Should().BeFalse();
        (await OutboxJobExistsAsync(cutoffProcessedJobId)).Should().BeTrue();
        (await OutboxJobExistsAsync(recentProcessedJobId)).Should().BeTrue();
    }

    [Fact]
    public async Task DeleteExpiredDevicesBatchAsync_ShouldRespectGracePeriodAndBatchSize()
    {
        var user = await AuthTestHelper.RegisterAsync(_client);
        var oldFirstDeviceId = Guid.NewGuid();
        var oldSecondDeviceId = Guid.NewGuid();
        var cutoffDeviceId = Guid.NewGuid();
        var recentDeviceId = Guid.NewGuid();
        var cutoffUtc = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await DeleteExpiredDevicesUntilEmptyAsync(cutoffUtc);
        await InsertDeviceAsync(oldFirstDeviceId, user.UserId, cutoffUtc.AddDays(-2));
        await InsertDeviceAsync(oldSecondDeviceId, user.UserId, cutoffUtc.AddDays(-1));
        await InsertDeviceAsync(cutoffDeviceId, user.UserId, cutoffUtc);
        await InsertDeviceAsync(recentDeviceId, user.UserId, cutoffUtc.AddDays(1));

        var firstBatch = await DeleteExpiredDevicesBatchAsync(cutoffUtc, batchSize: 1);
        var secondBatch = await DeleteExpiredDevicesBatchAsync(cutoffUtc, batchSize: 1);
        var thirdBatch = await DeleteExpiredDevicesBatchAsync(cutoffUtc, batchSize: 1);

        firstBatch.Should().Be(1);
        secondBatch.Should().Be(1);
        thirdBatch.Should().Be(0);
        (await NotificationDeviceExistsAsync(oldFirstDeviceId)).Should().BeFalse();
        (await NotificationDeviceExistsAsync(oldSecondDeviceId)).Should().BeFalse();
        (await NotificationDeviceExistsAsync(cutoffDeviceId)).Should().BeTrue();
        (await NotificationDeviceExistsAsync(recentDeviceId)).Should().BeTrue();
    }

    [Fact]
    public async Task DeleteExpiredDevicesBatchAsync_WhenOldestRowIsLocked_ShouldDeleteAnotherEligibleRow()
    {
        var user = await AuthTestHelper.RegisterAsync(_client);
        var lockedDeviceId = Guid.NewGuid();
        var unlockedDeviceId = Guid.NewGuid();
        var cutoffUtc = new DateTime(1800, 1, 3, 0, 0, 0, DateTimeKind.Utc);
        await DeleteExpiredDevicesUntilEmptyAsync(cutoffUtc);
        await InsertDeviceAsync(lockedDeviceId, user.UserId, cutoffUtc.AddDays(-2));
        await InsertDeviceAsync(unlockedDeviceId, user.UserId, cutoffUtc.AddDays(-1));

        await using var lockingScope = _factory.Services.CreateAsyncScope();
        var lockingSession = lockingScope.ServiceProvider.GetRequiredService<DbSession>();
        await lockingSession.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var lockingConnection = await lockingSession.GetOpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var lockCommand = lockingConnection.CreateCommand();
        lockCommand.CommandText = "SELECT id FROM notification_devices WHERE id = @DeviceId FOR UPDATE";
        lockCommand.Parameters.AddWithValue("DeviceId", lockedDeviceId);
        await lockCommand.ExecuteScalarAsync(TestContext.Current.CancellationToken);

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));

            await using var cleanupScope = _factory.Services.CreateAsyncScope();
            var repository = cleanupScope.ServiceProvider.GetRequiredService<INotificationCleanupRepository>();
            var deletedCount = await repository.DeleteExpiredDevicesBatchAsync(
                cutoffUtc,
                batchSize: 1,
                cancellationToken: timeout.Token);

            deletedCount.Should().Be(1);
            (await NotificationDeviceExistsAsync(lockedDeviceId)).Should().BeTrue();
            (await NotificationDeviceExistsAsync(unlockedDeviceId)).Should().BeFalse();
        }
        finally
        {
            await lockingSession.RollbackAsync(TestContext.Current.CancellationToken);
        }
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

    private async Task DeleteOutboxUntilEmptyAsync(string status, DateTime cutoffUtc)
    {
        while (await DeleteOutboxBatchAsync(status, cutoffUtc, batchSize: 100) > 0)
        {
        }
    }

    private async Task DeleteExpiredDevicesUntilEmptyAsync(DateTime cutoffUtc)
    {
        while (await DeleteExpiredDevicesBatchAsync(cutoffUtc, batchSize: 100) > 0)
        {
        }
    }

    private async Task ReplaceOutboxWithCleanupJobsAsync(
        Guid messageId,
        IReadOnlyList<(Guid Id, string Status, DateTime ProcessedAtUtc)> jobs)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbSession = scope.ServiceProvider.GetRequiredService<DbSession>();
        var connection = await dbSession.GetOpenConnectionAsync(TestContext.Current.CancellationToken);
        await dbSession.BeginTransactionAsync(TestContext.Current.CancellationToken);

        try
        {
            await using var deleteCommand = connection.CreateCommand();
            deleteCommand.Transaction = dbSession.Transaction;
            deleteCommand.CommandText = "DELETE FROM message_notification_outbox WHERE message_id = @MessageId";
            deleteCommand.Parameters.AddWithValue("MessageId", messageId);
            await deleteCommand.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

            foreach (var job in jobs)
            {
                await using var insertCommand = connection.CreateCommand();
                insertCommand.Transaction = dbSession.Transaction;
                insertCommand.CommandText = """
                                            INSERT INTO message_notification_outbox (
                                                id,
                                                message_id,
                                                status,
                                                attempts,
                                                next_attempt_at_utc,
                                                locked_until_utc,
                                                last_error,
                                                created_at_utc,
                                                processed_at_utc)
                                            VALUES (
                                                @Id,
                                                @MessageId,
                                                @Status,
                                                1,
                                                @ProcessedAtUtc,
                                                NULL,
                                                NULL,
                                                @ProcessedAtUtc,
                                                @ProcessedAtUtc)
                                            """;
                insertCommand.Parameters.AddWithValue("Id", job.Id);
                insertCommand.Parameters.AddWithValue("MessageId", messageId);
                insertCommand.Parameters.AddWithValue("Status", job.Status);
                insertCommand.Parameters.AddWithValue("ProcessedAtUtc", job.ProcessedAtUtc);
                await insertCommand.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }

            await dbSession.CommitAsync(TestContext.Current.CancellationToken);
        }
        catch
        {
            await dbSession.RollbackAsync(TestContext.Current.CancellationToken);
            throw;
        }
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

    private async Task<bool> OutboxJobExistsAsync(Guid jobId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbSession = scope.ServiceProvider.GetRequiredService<DbSession>();
        var connection = await dbSession.GetOpenConnectionAsync(TestContext.Current.CancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS (SELECT 1 FROM message_notification_outbox WHERE id = @JobId)";
        command.Parameters.AddWithValue("JobId", jobId);

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
