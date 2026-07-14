using Dapper;
using Harmonie.Application.Interfaces.Notifications;
using Harmonie.Domain.ValueObjects.Users;
using Harmonie.Infrastructure.Persistence.Common;

namespace Harmonie.Infrastructure.Persistence.Notifications;

public sealed class NotificationDeviceRepository : INotificationDeviceRepository
{
    private readonly DbSession _dbSession;
    private readonly TimeProvider _timeProvider;

    public NotificationDeviceRepository(DbSession dbSession, TimeProvider timeProvider)
    {
        _dbSession = dbSession;
        _timeProvider = timeProvider;
    }

    public async Task UpsertWebPushAsync(
        WebPushNotificationDeviceRegistration registration,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
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
                               @Platform,
                               @Token,
                               @WebPushP256dh,
                               @WebPushAuth,
                               @ExpiresAtUtc,
                               @NowUtc,
                               @NowUtc)
                           ON CONFLICT (platform, token)
                           DO UPDATE SET
                               user_id = EXCLUDED.user_id,
                               web_push_p256dh = EXCLUDED.web_push_p256dh,
                               web_push_auth = EXCLUDED.web_push_auth,
                               expires_at_utc = EXCLUDED.expires_at_utc,
                               updated_at_utc = EXCLUDED.updated_at_utc
                           """;

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                Id = Guid.NewGuid(),
                UserId = registration.UserId.Value,
                Platform = NotificationDevicePlatforms.WebPush,
                Token = registration.Endpoint,
                WebPushP256dh = registration.P256dh,
                WebPushAuth = registration.Auth,
                registration.ExpiresAtUtc,
                NowUtc = nowUtc
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    public async Task<IReadOnlyList<NotificationDevice>> GetActiveByUserIdsAsync(
        IReadOnlyCollection<UserId> userIds,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
            return [];

        const string sql = """
                           SELECT
                               id AS "Id",
                               user_id AS "UserId",
                               platform AS "Platform",
                               token AS "Token",
                               web_push_p256dh AS "WebPushP256dh",
                               web_push_auth AS "WebPushAuth",
                               expires_at_utc AS "ExpiresAtUtc"
                           FROM notification_devices
                           WHERE user_id = ANY(@UserIds)
                             AND (expires_at_utc IS NULL OR expires_at_utc > @NowUtc)
                           ORDER BY updated_at_utc ASC, id ASC
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                UserIds = userIds.Select(userId => userId.Value).ToArray(),
                NowUtc = nowUtc
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        var rows = await connection.QueryAsync<NotificationDeviceRow>(command);
        return rows
            .Select(row => new NotificationDevice(
                row.Id,
                UserId.From(row.UserId),
                row.Platform,
                row.Token,
                row.WebPushP256dh,
                row.WebPushAuth,
                row.ExpiresAtUtc))
            .ToArray();
    }

    public Task DeleteAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        return DeleteManyAsync([deviceId], cancellationToken);
    }

    public async Task DeleteManyAsync(
        IReadOnlyCollection<Guid> deviceIds,
        CancellationToken cancellationToken = default)
    {
        if (deviceIds.Count == 0)
            return;

        const string sql = """
                           DELETE FROM notification_devices
                           WHERE id = ANY(@DeviceIds)
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { DeviceIds = deviceIds.ToArray() },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    private sealed class NotificationDeviceRow
    {
        public Guid Id { get; init; }

        public Guid UserId { get; init; }

        public string Platform { get; init; } = string.Empty;

        public string Token { get; init; } = string.Empty;

        public string? WebPushP256dh { get; init; }

        public string? WebPushAuth { get; init; }

        public DateTime? ExpiresAtUtc { get; init; }
    }
}
