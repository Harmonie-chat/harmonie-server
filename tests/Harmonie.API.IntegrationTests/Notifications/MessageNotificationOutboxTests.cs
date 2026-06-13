using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Features.Channels.SendMessage;
using Harmonie.Application.Interfaces.Notifications;
using Harmonie.Infrastructure.Persistence.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Harmonie.API.IntegrationTests.Notifications;

public sealed class MessageNotificationOutboxTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HarmonieWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public MessageNotificationOutboxTests(HarmonieWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SendChannelMessage_WhenSuccessful_ShouldCreatePendingOutboxJob()
    {
        var user = await AuthTestHelper.RegisterAsync(_client);
        var (_, channelId) = await ChannelTestHelper.CreateGuildAndChannelAsync(_client, user.AccessToken);

        var message = await ChannelTestHelper.SendChannelMessageAsync(
            _client,
            channelId,
            "hello channel outbox",
            user.AccessToken);

        var job = await GetOutboxJobAsync(message.MessageId);
        job.Should().NotBeNull();
        job!.MessageId.Should().Be(message.MessageId);
        job.Status.Should().Be(MessageNotificationOutboxStatuses.Pending);
        job.Attempts.Should().Be(0);
        job.ProcessedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task SendConversationMessage_WhenSuccessful_ShouldCreatePendingOutboxJob()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var target = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, caller.AccessToken, target.UserId);

        var message = await ConversationTestHelper.SendConversationMessageAsync(
            _client,
            conversationId,
            "hello conversation outbox",
            caller.AccessToken);

        var job = await GetOutboxJobAsync(message.MessageId);
        job.Should().NotBeNull();
        job!.MessageId.Should().Be(message.MessageId);
        job.Status.Should().Be(MessageNotificationOutboxStatuses.Pending);
        job.Attempts.Should().Be(0);
        job.ProcessedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task SendChannelMessage_WhenRequestFails_ShouldNotCreateOutboxJob()
    {
        var user = await AuthTestHelper.RegisterAsync(_client);
        var (_, channelId) = await ChannelTestHelper.CreateGuildAndChannelAsync(_client, user.AccessToken);
        var uniqueContent = $"failed outbox marker {Guid.NewGuid():N}";

        var response = await _client.SendAuthorizedPostAsync(
            $"/api/channels/{channelId}/messages",
            new SendMessageRequest(uniqueContent, ReplyToMessageId: Guid.NewGuid()),
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var count = await CountOutboxJobsForMessageContentAsync(uniqueContent);
        count.Should().Be(0);
    }

    [Fact]
    public async Task ClaimPendingAsync_WhenJobIsClaimed_ShouldNotReturnItAgainWhileLocked()
    {
        var user = await AuthTestHelper.RegisterAsync(_client);
        var (_, channelId) = await ChannelTestHelper.CreateGuildAndChannelAsync(_client, user.AccessToken);
        var message = await ChannelTestHelper.SendChannelMessageAsync(
            _client,
            channelId,
            "claim me once",
            user.AccessToken);
        await DelayOtherPendingOutboxJobsAsync(message.MessageId, DateTime.UtcNow.AddDays(1));
        await SetOutboxNextAttemptAsync(message.MessageId, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var firstClaim = await ClaimPendingAsync(batchSize: 1);
        firstClaim.Should().ContainSingle(job => job.MessageId.Value == message.MessageId);

        var secondClaim = await ClaimPendingAsync(batchSize: 10);
        secondClaim.Should().NotContain(job => job.MessageId.Value == message.MessageId);

        var stored = await GetOutboxJobAsync(message.MessageId);
        stored.Should().NotBeNull();
        stored!.Status.Should().Be(MessageNotificationOutboxStatuses.Processing);
        stored.Attempts.Should().Be(1);
        stored.LockedUntilUtc.Should().NotBeNull();
    }

    private async Task<IReadOnlyList<MessageNotificationOutboxJob>> ClaimPendingAsync(int batchSize)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IMessageNotificationOutboxRepository>();
        return await repository.ClaimPendingAsync(
            batchSize,
            DateTime.UtcNow.AddMinutes(1),
            TimeSpan.FromMinutes(5),
            TestContext.Current.CancellationToken);
    }

    private async Task DelayOtherPendingOutboxJobsAsync(Guid messageId, DateTime nextAttemptAtUtc)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbSession = scope.ServiceProvider.GetRequiredService<DbSession>();
        var connection = await dbSession.GetOpenConnectionAsync(TestContext.Current.CancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
                              UPDATE message_notification_outbox
                              SET next_attempt_at_utc = @NextAttemptAtUtc
                              WHERE message_id <> @MessageId
                                AND status = @PendingStatus
                              """;
        command.Parameters.AddWithValue("NextAttemptAtUtc", nextAttemptAtUtc);
        command.Parameters.AddWithValue("MessageId", messageId);
        command.Parameters.AddWithValue("PendingStatus", MessageNotificationOutboxStatuses.Pending);

        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private async Task SetOutboxNextAttemptAsync(Guid messageId, DateTime nextAttemptAtUtc)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbSession = scope.ServiceProvider.GetRequiredService<DbSession>();
        var connection = await dbSession.GetOpenConnectionAsync(TestContext.Current.CancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
                              UPDATE message_notification_outbox
                              SET next_attempt_at_utc = @NextAttemptAtUtc
                              WHERE message_id = @MessageId
                              """;
        command.Parameters.AddWithValue("NextAttemptAtUtc", nextAttemptAtUtc);
        command.Parameters.AddWithValue("MessageId", messageId);

        var rows = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        rows.Should().Be(1);
    }

    private async Task<StoredOutboxJob?> GetOutboxJobAsync(Guid messageId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbSession = scope.ServiceProvider.GetRequiredService<DbSession>();
        var connection = await dbSession.GetOpenConnectionAsync(TestContext.Current.CancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT message_id, status, attempts, locked_until_utc, processed_at_utc
                              FROM message_notification_outbox
                              WHERE message_id = @MessageId
                              """;
        command.Parameters.AddWithValue("MessageId", messageId);

        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        if (!await reader.ReadAsync(TestContext.Current.CancellationToken))
            return null;

        return new StoredOutboxJob(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetInt32(2),
            reader.IsDBNull(3) ? null : reader.GetDateTime(3),
            reader.IsDBNull(4) ? null : reader.GetDateTime(4));
    }

    private async Task<long> CountOutboxJobsForMessageContentAsync(string content)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbSession = scope.ServiceProvider.GetRequiredService<DbSession>();
        var connection = await dbSession.GetOpenConnectionAsync(TestContext.Current.CancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT COUNT(*)
                              FROM message_notification_outbox outbox
                              JOIN messages m ON m.id = outbox.message_id
                              WHERE m.content = @Content
                              """;
        command.Parameters.AddWithValue("Content", content);

        var result = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        result.Should().BeOfType<long>();
        return (long)result;
    }

    private sealed record StoredOutboxJob(
        Guid MessageId,
        string Status,
        int Attempts,
        DateTime? LockedUntilUtc,
        DateTime? ProcessedAtUtc);
}
