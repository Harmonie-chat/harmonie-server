using Dapper;
using Harmonie.Application.Interfaces.Notifications;
using Harmonie.Infrastructure.Persistence.Common;

namespace Harmonie.Infrastructure.Persistence.Notifications;

public sealed class NotificationDeviceRepository : INotificationDeviceRepository
{
    private const string WebPushPlatform = "web_push";

    private readonly DbSession _dbSession;

    public NotificationDeviceRepository(DbSession dbSession)
    {
        _dbSession = dbSession;
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

        var nowUtc = DateTime.UtcNow;
        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                Id = Guid.NewGuid(),
                UserId = registration.UserId.Value,
                Platform = WebPushPlatform,
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
}
