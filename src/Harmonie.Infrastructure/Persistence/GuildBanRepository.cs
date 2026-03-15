using Dapper;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.ValueObjects;
using Harmonie.Infrastructure.Rows;
using Npgsql;

namespace Harmonie.Infrastructure.Persistence;

public sealed class GuildBanRepository : IGuildBanRepository
{
    private readonly DbSession _dbSession;

    public GuildBanRepository(DbSession dbSession)
    {
        _dbSession = dbSession;
    }

    public async Task<bool> TryAddAsync(
        GuildBan ban,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           INSERT INTO guild_bans (
                               guild_id,
                               user_id,
                               reason,
                               banned_by,
                               created_at_utc)
                           VALUES (
                               @GuildId,
                               @UserId,
                               @Reason,
                               @BannedBy,
                               @CreatedAtUtc)
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                GuildId = ban.GuildId.Value,
                UserId = ban.UserId.Value,
                ban.Reason,
                BannedBy = ban.BannedBy.Value,
                ban.CreatedAtUtc
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        try
        {
            await connection.ExecuteAsync(command);
            return true;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return false;
        }
    }

    public async Task<bool> ExistsAsync(
        GuildId guildId,
        UserId userId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT EXISTS(
                               SELECT 1
                               FROM guild_bans
                               WHERE guild_id = @GuildId
                                 AND user_id = @UserId)
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                GuildId = guildId.Value,
                UserId = userId.Value
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        return await connection.ExecuteScalarAsync<bool>(command);
    }

    public async Task<bool> DeleteAsync(
        GuildId guildId,
        UserId userId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           DELETE FROM guild_bans
                           WHERE guild_id = @GuildId
                             AND user_id = @UserId
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                GuildId = guildId.Value,
                UserId = userId.Value
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        var affected = await connection.ExecuteAsync(command);
        return affected > 0;
    }

    public async Task<IReadOnlyList<GuildBanWithUser>> GetByGuildIdAsync(
        GuildId guildId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT gb.user_id AS "UserId",
                                  u.username AS "Username",
                                  u.display_name AS "DisplayName",
                                  u.avatar_file_id AS "AvatarFileId",
                                  u.avatar_color AS "AvatarColor",
                                  u.avatar_icon AS "AvatarIcon",
                                  u.avatar_bg AS "AvatarBg",
                                  gb.reason AS "Reason",
                                  gb.banned_by AS "BannedBy",
                                  gb.created_at_utc AS "CreatedAtUtc"
                           FROM guild_bans gb
                           INNER JOIN users u ON u.id = gb.user_id
                           WHERE gb.guild_id = @GuildId
                           ORDER BY gb.created_at_utc DESC
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { GuildId = guildId.Value },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        var rows = await connection.QueryAsync<GuildBanWithUserRow>(command);
        return rows.Select(MapToGuildBanWithUser).ToArray();
    }

    private static GuildBanWithUser MapToGuildBanWithUser(GuildBanWithUserRow row)
    {
        var usernameResult = Username.Create(row.Username);
        if (usernameResult.IsFailure || usernameResult.Value is null)
            throw new InvalidOperationException("Stored username is invalid.");

        return new GuildBanWithUser(
            UserId.From(row.UserId),
            usernameResult.Value,
            row.DisplayName,
            row.AvatarFileId.HasValue ? UploadedFileId.From(row.AvatarFileId.Value) : null,
            row.AvatarColor,
            row.AvatarIcon,
            row.AvatarBg,
            row.Reason,
            UserId.From(row.BannedBy),
            row.CreatedAtUtc);
    }
}
