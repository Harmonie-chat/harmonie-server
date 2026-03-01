using Dapper;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.ValueObjects;
using Harmonie.Infrastructure.Dto;

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

        var row = await connection.QueryFirstOrDefaultAsync<GuildDto>(command);
        return row is null ? null : MapToGuild(row);
    }

    public async Task AddAsync(Guild guild, CancellationToken cancellationToken = default)
    {
        const string sql = """
                           INSERT INTO guilds (
                               id,
                               name,
                               owner_user_id,
                               created_at_utc,
                               updated_at_utc)
                           VALUES (
                               @Id,
                               @Name,
                               @OwnerUserId,
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
                guild.CreatedAtUtc,
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

    private static Guild MapToGuild(GuildDto row)
    {
        var nameResult = GuildName.Create(row.Name);
        if (nameResult.IsFailure || nameResult.Value is null)
            throw new InvalidOperationException("Stored guild name is invalid.");

        return Guild.Rehydrate(
            GuildId.From(row.Id),
            nameResult.Value,
            UserId.From(row.OwnerUserId),
            row.CreatedAtUtc,
            row.UpdatedAtUtc);
    }
}
