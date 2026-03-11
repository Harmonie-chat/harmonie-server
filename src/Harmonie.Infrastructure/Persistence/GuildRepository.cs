using Dapper;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Harmonie.Infrastructure.Rows;

namespace Harmonie.Infrastructure.Persistence;

public sealed class GuildRepository : IGuildRepository
{
    private readonly DbSession _dbSession;

    public GuildRepository(DbSession dbSession)
    {
        _dbSession = dbSession;
    }

    public async Task<Guild?> GetByIdAsync(GuildId guildId, CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT id AS "Id",
                                  name AS "Name",
                                  owner_user_id AS "OwnerUserId",
                                  icon_file_id AS "IconFileId",
                                  icon_color AS "IconColor",
                                  icon_name AS "IconName",
                                  icon_bg AS "IconBg",
                                  created_at_utc AS "CreatedAtUtc",
                                  updated_at_utc AS "UpdatedAtUtc"
                           FROM guilds
                           WHERE id = @GuildId
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { GuildId = guildId.Value },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        var row = await connection.QueryFirstOrDefaultAsync<GuildRow>(command);
        return row is null ? null : MapToGuild(row);
    }

    public async Task<GuildAccessContext?> GetWithCallerRoleAsync(
        GuildId guildId,
        UserId callerId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT g.id             AS "Id",
                                  g.name           AS "Name",
                                  g.owner_user_id  AS "OwnerUserId",
                                  g.icon_file_id   AS "IconFileId",
                                  g.icon_color     AS "IconColor",
                                  g.icon_name      AS "IconName",
                                  g.icon_bg        AS "IconBg",
                                  g.created_at_utc AS "CreatedAtUtc",
                                  g.updated_at_utc AS "UpdatedAtUtc",
                                  gm.role          AS "Role"
                           FROM guilds g
                           LEFT JOIN guild_members gm
                                  ON gm.guild_id = g.id
                                 AND gm.user_id = @CallerId
                           WHERE g.id = @GuildId
                           LIMIT 1
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { GuildId = guildId.Value, CallerId = callerId.Value },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        var row = await connection.QueryFirstOrDefaultAsync<GuildWithRoleRow>(command);
        if (row is null)
            return null;

        if (row.Role.HasValue && !Enum.IsDefined(typeof(GuildRole), row.Role.Value))
            throw new InvalidOperationException("Stored guild role is invalid.");

        return new GuildAccessContext(
            MapToGuild(row),
            row.Role.HasValue ? (GuildRole)row.Role.Value : null);
    }

    public async Task AddAsync(Guild guild, CancellationToken cancellationToken = default)
    {
        const string sql = """
                           INSERT INTO guilds (
                               id,
                               name,
                               owner_user_id,
                               icon_url,
                               icon_file_id,
                               icon_color,
                               icon_name,
                               icon_bg,
                               created_at_utc,
                               updated_at_utc)
                           VALUES (
                               @Id,
                               @Name,
                               @OwnerUserId,
                               NULL,
                               @IconFileId,
                               @IconColor,
                               @IconName,
                               @IconBg,
                               @CreatedAtUtc,
                               @UpdatedAtUtc)
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                Id = guild.Id.Value,
                Name = guild.Name.Value,
                OwnerUserId = guild.OwnerUserId.Value,
                IconFileId = guild.IconFileId?.Value,
                guild.IconColor,
                guild.IconName,
                guild.IconBg,
                guild.CreatedAtUtc,
                UpdatedAtUtc = guild.UpdatedAtUtc ?? guild.CreatedAtUtc
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    public async Task UpdateAsync(Guild guild, CancellationToken cancellationToken = default)
    {
        const string sql = """
                           UPDATE guilds
                           SET name = @Name,
                               icon_url = NULL,
                               icon_file_id = @IconFileId,
                               icon_color = @IconColor,
                               icon_name = @IconName,
                               icon_bg = @IconBg,
                               updated_at_utc = @UpdatedAtUtc
                           WHERE id = @Id
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                Id = guild.Id.Value,
                Name = guild.Name.Value,
                IconFileId = guild.IconFileId?.Value,
                guild.IconColor,
                guild.IconName,
                guild.IconBg,
                UpdatedAtUtc = guild.UpdatedAtUtc ?? guild.CreatedAtUtc
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    public async Task<bool> ExistsAsync(GuildId guildId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT EXISTS(SELECT 1 FROM guilds WHERE id = @GuildId)";

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { GuildId = guildId.Value },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        return await connection.ExecuteScalarAsync<bool>(command);
    }

    public async Task UpdateOwnerAsync(GuildId guildId, UserId newOwnerId, CancellationToken cancellationToken = default)
    {
        const string sql = """
                           UPDATE guilds
                           SET owner_user_id = @NewOwnerId,
                               updated_at_utc = NOW()
                           WHERE id = @GuildId
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                GuildId = guildId.Value,
                NewOwnerId = newOwnerId.Value
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    private static Guild MapToGuild(GuildRow row)
    {
        var nameResult = GuildName.Create(row.Name);
        if (nameResult.IsFailure || nameResult.Value is null)
            throw new InvalidOperationException("Stored guild name is invalid.");

        return Guild.Rehydrate(
            GuildId.From(row.Id),
            nameResult.Value,
            UserId.From(row.OwnerUserId),
            row.CreatedAtUtc,
            row.UpdatedAtUtc,
            row.IconFileId.HasValue ? UploadedFileId.From(row.IconFileId.Value) : null,
            row.IconColor,
            row.IconName,
            row.IconBg);
    }

    private static Guild MapToGuild(GuildWithRoleRow row)
    {
        var nameResult = GuildName.Create(row.Name);
        if (nameResult.IsFailure || nameResult.Value is null)
            throw new InvalidOperationException("Stored guild name is invalid.");

        return Guild.Rehydrate(
            GuildId.From(row.Id),
            nameResult.Value,
            UserId.From(row.OwnerUserId),
            row.CreatedAtUtc,
            row.UpdatedAtUtc,
            row.IconFileId.HasValue ? UploadedFileId.From(row.IconFileId.Value) : null,
            row.IconColor,
            row.IconName,
            row.IconBg);
    }

    private sealed class GuildWithRoleRow
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public Guid OwnerUserId { get; init; }
        public Guid? IconFileId { get; init; }
        public string? IconColor { get; init; }
        public string? IconName { get; init; }
        public string? IconBg { get; init; }
        public DateTime CreatedAtUtc { get; init; }
        public DateTime UpdatedAtUtc { get; init; }
        public short? Role { get; init; }
    }
}
