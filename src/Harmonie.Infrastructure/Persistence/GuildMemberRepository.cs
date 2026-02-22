using Dapper;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Npgsql;

namespace Harmonie.Infrastructure.Persistence;

public sealed class GuildMemberRepository : IGuildMemberRepository
{
    private readonly DbSession _dbSession;

    public GuildMemberRepository(DbSession dbSession)
    {
        _dbSession = dbSession;
    }

    public async Task<bool> IsMemberAsync(
        GuildId guildId,
        UserId userId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT EXISTS(
                               SELECT 1
                               FROM guild_members
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

    public async Task<GuildRole?> GetRoleAsync(
        GuildId guildId,
        UserId userId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT role
                           FROM guild_members
                           WHERE guild_id = @GuildId
                             AND user_id = @UserId
                           LIMIT 1
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

        var role = await connection.QueryFirstOrDefaultAsync<short?>(command);
        if (role is null)
            return null;

        if (!Enum.IsDefined(typeof(GuildRole), role.Value))
            throw new InvalidOperationException("Stored guild role is invalid.");

        return (GuildRole)role.Value;
    }

    public async Task<bool> TryAddAsync(
        GuildMember member,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           INSERT INTO guild_members (
                               guild_id,
                               user_id,
                               role,
                               joined_at_utc,
                               invited_by_user_id)
                           VALUES (
                               @GuildId,
                               @UserId,
                               @Role,
                               @JoinedAtUtc,
                               @InvitedByUserId)
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                GuildId = member.GuildId.Value,
                UserId = member.UserId.Value,
                Role = (short)member.Role,
                member.JoinedAtUtc,
                InvitedByUserId = member.InvitedByUserId?.Value
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
}
