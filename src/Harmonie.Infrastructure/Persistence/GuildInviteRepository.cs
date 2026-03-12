using Dapper;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;

namespace Harmonie.Infrastructure.Persistence;

public sealed class GuildInviteRepository : IGuildInviteRepository
{
    private readonly DbSession _dbSession;

    public GuildInviteRepository(DbSession dbSession)
    {
        _dbSession = dbSession;
    }

    public async Task AddAsync(GuildInvite invite, CancellationToken cancellationToken = default)
    {
        const string sql = """
                           INSERT INTO guild_invites (
                               id,
                               code,
                               guild_id,
                               creator_id,
                               max_uses,
                               uses_count,
                               expires_at_utc,
                               created_at_utc)
                           VALUES (
                               @Id,
                               @Code,
                               @GuildId,
                               @CreatorId,
                               @MaxUses,
                               @UsesCount,
                               @ExpiresAtUtc,
                               @CreatedAtUtc)
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                Id = invite.Id.Value,
                invite.Code,
                GuildId = invite.GuildId.Value,
                CreatorId = invite.CreatorId.Value,
                invite.MaxUses,
                invite.UsesCount,
                invite.ExpiresAtUtc,
                invite.CreatedAtUtc
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }
}
