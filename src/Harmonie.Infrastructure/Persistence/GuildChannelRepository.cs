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
}
