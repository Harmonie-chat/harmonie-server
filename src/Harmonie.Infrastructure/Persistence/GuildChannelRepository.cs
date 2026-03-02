using Dapper;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Harmonie.Infrastructure.Dto;

namespace Harmonie.Infrastructure.Persistence;

public sealed class GuildChannelRepository : IGuildChannelRepository
{
    private readonly DbSession _dbSession;

    public GuildChannelRepository(DbSession dbSession)
    {
        _dbSession = dbSession;
    }

    public async Task<GuildChannel?> GetByIdAsync(
        GuildChannelId channelId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT id AS "Id",
                                  guild_id AS "GuildId",
                                  name AS "Name",
                                  type AS "Type",
                                  is_default AS "IsDefault",
                                  position AS "Position",
                                  created_at_utc AS "CreatedAtUtc"
                           FROM guild_channels
                           WHERE id = @ChannelId
                           LIMIT 1
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { ChannelId = channelId.Value },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        var row = await connection.QueryFirstOrDefaultAsync<GuildChannelDto>(command);
        return row is null ? null : MapToGuildChannel(row);
    }

    public async Task AddAsync(
        GuildChannel channel,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           INSERT INTO guild_channels (
                               id,
                               guild_id,
                               name,
                               type,
                               is_default,
                               position,
                               created_at_utc)
                           VALUES (
                               @Id,
                               @GuildId,
                               @Name,
                               @Type,
                               @IsDefault,
                               @Position,
                               @CreatedAtUtc)
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                Id = channel.Id.Value,
                GuildId = channel.GuildId.Value,
                channel.Name,
                Type = (short)channel.Type,
                channel.IsDefault,
                channel.Position,
                channel.CreatedAtUtc
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    public async Task<IReadOnlyList<GuildChannel>> GetByGuildIdAsync(
        GuildId guildId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT id AS "Id",
                                  guild_id AS "GuildId",
                                  name AS "Name",
                                  type AS "Type",
                                  is_default AS "IsDefault",
                                  position AS "Position",
                                  created_at_utc AS "CreatedAtUtc"
                           FROM guild_channels
                           WHERE guild_id = @GuildId
                           ORDER BY position ASC, created_at_utc ASC, id ASC
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { GuildId = guildId.Value },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        var rows = await connection.QueryAsync<GuildChannelDto>(command);
        return rows.Select(MapToGuildChannel).ToArray();
    }

    public async Task UpdateAsync(
        GuildChannel channel,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           UPDATE guild_channels
                           SET name     = @Name,
                               position = @Position
                           WHERE id = @Id
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                Id = channel.Id.Value,
                channel.Name,
                channel.Position
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    public async Task DeleteAsync(
        GuildChannelId channelId,
        CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM guild_channels WHERE id = @ChannelId";
        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { ChannelId = channelId.Value },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    public async Task<bool> ExistsByNameInGuildAsync(
        GuildId guildId,
        string name,
        GuildChannelId excludeId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT COUNT(1)
                           FROM guild_channels
                           WHERE guild_id = @GuildId
                             AND name     = @Name
                             AND id      != @ExcludeId
                           LIMIT 1
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                GuildId = guildId.Value,
                Name = name,
                ExcludeId = excludeId.Value
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        var count = await connection.ExecuteScalarAsync<int>(command);
        return count > 0;
    }

    public async Task<ChannelAccessContext?> GetWithCallerRoleAsync(
        GuildChannelId channelId,
        UserId callerId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT gc.id             AS "Id",
                                  gc.guild_id       AS "GuildId",
                                  gc.name           AS "Name",
                                  gc.type           AS "Type",
                                  gc.is_default     AS "IsDefault",
                                  gc.position       AS "Position",
                                  gc.created_at_utc AS "CreatedAtUtc",
                                  gm.role           AS "Role"
                           FROM guild_channels gc
                           LEFT JOIN guild_members gm
                                  ON gm.guild_id = gc.guild_id
                                 AND gm.user_id = @CallerId
                           WHERE gc.id = @ChannelId
                           LIMIT 1
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { ChannelId = channelId.Value, CallerId = callerId.Value },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        var row = await connection.QueryFirstOrDefaultAsync<ChannelWithRoleDto>(command);
        return row is null
            ? null
            : new ChannelAccessContext(
                MapToGuildChannel(row),
                row.Role.HasValue ? (GuildRole)row.Role.Value : null);
    }

    private static GuildChannel MapToGuildChannel(GuildChannelDto row)
    {
        if (!Enum.IsDefined(typeof(GuildChannelType), row.Type))
            throw new InvalidOperationException("Stored channel type is invalid.");

        return GuildChannel.Rehydrate(
            GuildChannelId.From(row.Id),
            GuildId.From(row.GuildId),
            row.Name,
            (GuildChannelType)row.Type,
            row.IsDefault,
            row.Position,
            row.CreatedAtUtc);
    }

    private static GuildChannel MapToGuildChannel(ChannelWithRoleDto row)
    {
        if (!Enum.IsDefined(typeof(GuildChannelType), row.Type))
            throw new InvalidOperationException("Stored channel type is invalid.");

        return GuildChannel.Rehydrate(
            GuildChannelId.From(row.Id),
            GuildId.From(row.GuildId),
            row.Name,
            (GuildChannelType)row.Type,
            row.IsDefault,
            row.Position,
            row.CreatedAtUtc);
    }

    private sealed class ChannelWithRoleDto
    {
        public Guid Id { get; init; }
        public Guid GuildId { get; init; }
        public string Name { get; init; } = string.Empty;
        public short Type { get; init; }
        public bool IsDefault { get; init; }
        public int Position { get; init; }
        public DateTime CreatedAtUtc { get; init; }
        public short? Role { get; init; }
    }
}
