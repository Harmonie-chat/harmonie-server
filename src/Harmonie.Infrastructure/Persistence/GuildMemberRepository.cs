using Dapper;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Harmonie.Infrastructure.Dto;
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

    public async Task<IReadOnlyList<UserGuildMembership>> GetUserGuildMembershipsAsync(
        UserId userId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT g.id AS "GuildId",
                                  g.name AS "GuildName",
                                  g.owner_user_id AS "OwnerUserId",
                                  g.created_at_utc AS "GuildCreatedAtUtc",
                                  g.updated_at_utc AS "GuildUpdatedAtUtc",
                                  gm.role AS "Role",
                                  gm.joined_at_utc AS "JoinedAtUtc"
                           FROM guild_members gm
                           INNER JOIN guilds g ON g.id = gm.guild_id
                           WHERE gm.user_id = @UserId
                           ORDER BY gm.joined_at_utc DESC, gm.guild_id ASC
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { UserId = userId.Value },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        var rows = await connection.QueryAsync<UserGuildMembershipDto>(command);
        return rows.Select(MapToUserGuildMembership).ToArray();
    }

    public async Task<IReadOnlyList<GuildMemberUser>> GetGuildMembersAsync(
        GuildId guildId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT gm.user_id AS "UserId",
                                  u.username AS "Username",
                                  u.display_name AS "DisplayName",
                                  u.avatar_url AS "AvatarUrl",
                                  u.is_active AS "IsActive",
                                  gm.role AS "Role",
                                  gm.joined_at_utc AS "JoinedAtUtc"
                           FROM guild_members gm
                           INNER JOIN users u ON u.id = gm.user_id
                           WHERE gm.guild_id = @GuildId
                             AND u.deleted_at IS NULL
                           ORDER BY gm.joined_at_utc ASC, gm.user_id ASC
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { GuildId = guildId.Value },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        var rows = await connection.QueryAsync<GuildMemberUserDto>(command);
        return rows.Select(MapToGuildMemberUser).ToArray();
    }

    public async Task RemoveAsync(
        GuildId guildId,
        UserId userId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           DELETE FROM guild_members
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

        await connection.ExecuteAsync(command);
    }

    private static UserGuildMembership MapToUserGuildMembership(UserGuildMembershipDto row)
    {
        if (!Enum.IsDefined(typeof(GuildRole), row.Role))
            throw new InvalidOperationException("Stored guild role is invalid.");

        var guildNameResult = GuildName.Create(row.GuildName);
        if (guildNameResult.IsFailure || guildNameResult.Value is null)
            throw new InvalidOperationException("Stored guild name is invalid.");

        var guild = Guild.Rehydrate(
            GuildId.From(row.GuildId),
            guildNameResult.Value,
            UserId.From(row.OwnerUserId),
            row.GuildCreatedAtUtc,
            row.GuildUpdatedAtUtc);

        return new UserGuildMembership(
            guild,
            (GuildRole)row.Role,
            row.JoinedAtUtc);
    }

    private static GuildMemberUser MapToGuildMemberUser(GuildMemberUserDto row)
    {
        if (!Enum.IsDefined(typeof(GuildRole), row.Role))
            throw new InvalidOperationException("Stored guild role is invalid.");

        var usernameResult = Username.Create(row.Username);
        if (usernameResult.IsFailure || usernameResult.Value is null)
            throw new InvalidOperationException("Stored username is invalid.");

        return new GuildMemberUser(
            UserId.From(row.UserId),
            usernameResult.Value,
            row.DisplayName,
            row.AvatarUrl,
            row.IsActive,
            (GuildRole)row.Role,
            row.JoinedAtUtc);
    }
}
