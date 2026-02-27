using Dapper;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.ValueObjects;
using Harmonie.Infrastructure.Dto;
using Npgsql;

namespace Harmonie.Infrastructure.Persistence;

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly DbSession _dbSession;

    public RefreshTokenRepository(DbSession dbSession)
    {
        _dbSession = dbSession;
    }

    public async Task StoreAsync(
        UserId userId,
        string tokenHash,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO refresh_tokens (id, user_id, token_hash, created_at_utc, expires_at_utc)
            VALUES (@Id, @UserId, @TokenHash, @CreatedAtUtc, @ExpiresAtUtc)";

        var conn = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(
            sql,
            new
            {
                Id = Guid.NewGuid(),
                UserId = userId.Value,
                TokenHash = tokenHash,
                CreatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = expiresAtUtc
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        await conn.ExecuteAsync(cmd);
    }

    public async Task<RefreshTokenSession?> GetByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT id as ""Id"",
                   user_id as ""UserId"",
                   token_hash as ""TokenHash"",
                   expires_at_utc as ""ExpiresAtUtc"",
                   revoked_at_utc as ""RevokedAtUtc""
            FROM refresh_tokens
            WHERE token_hash = @TokenHash";

        var conn = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(
            sql,
            new { TokenHash = tokenHash },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);
        var tokenRow = await conn.QueryFirstOrDefaultAsync<RefreshTokenDto>(cmd);

        if (tokenRow is null)
            return null;

        return new RefreshTokenSession(
            Id: tokenRow.Id,
            UserId: UserId.From(tokenRow.UserId),
            ExpiresAtUtc: tokenRow.ExpiresAtUtc,
            RevokedAtUtc: tokenRow.RevokedAtUtc);
    }

    public async Task<bool> RotateAsync(
        Guid tokenId,
        UserId userId,
        string newTokenHash,
        DateTime newExpiresAtUtc,
        DateTime revokedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var conn = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var ambientTransaction = _dbSession.Transaction;

        if (ambientTransaction is not null)
            return await RotateInTransactionAsync(conn, ambientTransaction, tokenId, userId, newTokenHash, newExpiresAtUtc, revokedAtUtc, cancellationToken);

        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        try
        {
            var rotated = await RotateInTransactionAsync(conn, tx, tokenId, userId, newTokenHash, newExpiresAtUtc, revokedAtUtc, cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return rotated;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task<bool> RotateInTransactionAsync(
        NpgsqlConnection conn,
        NpgsqlTransaction tx,
        Guid tokenId,
        UserId userId,
        string newTokenHash,
        DateTime newExpiresAtUtc,
        DateTime revokedAtUtc,
        CancellationToken cancellationToken)
    {
        const string revokeSql = """
                                 UPDATE refresh_tokens
                                 SET revoked_at_utc = @RevokedAtUtc
                                 WHERE id = @Id
                                   AND revoked_at_utc IS NULL
                                 """;

        const string insertSql = """
                                 INSERT INTO refresh_tokens (id, user_id, token_hash, created_at_utc, expires_at_utc)
                                 VALUES (@Id, @UserId, @TokenHash, @CreatedAtUtc, @ExpiresAtUtc)
                                 """;

        var revokeCmd = new CommandDefinition(
            revokeSql,
            new { Id = tokenId, RevokedAtUtc = revokedAtUtc },
            transaction: tx,
            cancellationToken: cancellationToken);

        var affectedRows = await conn.ExecuteAsync(revokeCmd);
        if (affectedRows != 1)
            return false;

        var insertCmd = new CommandDefinition(
            insertSql,
            new
            {
                Id = Guid.NewGuid(),
                UserId = userId.Value,
                TokenHash = newTokenHash,
                CreatedAtUtc = revokedAtUtc,
                ExpiresAtUtc = newExpiresAtUtc
            },
            transaction: tx,
            cancellationToken: cancellationToken);

        await conn.ExecuteAsync(insertCmd);
        return true;
    }

    public async Task<bool> RevokeActiveAsync(
        UserId userId,
        string tokenHash,
        DateTime revokedAtUtc,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           UPDATE refresh_tokens
                           SET revoked_at_utc = @RevokedAtUtc
                           WHERE user_id = @UserId
                             AND token_hash = @TokenHash
                             AND revoked_at_utc IS NULL
                             AND expires_at_utc > @RevokedAtUtc
                           """;

        var conn = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(
            sql,
            new
            {
                UserId = userId.Value,
                TokenHash = tokenHash,
                RevokedAtUtc = revokedAtUtc
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        var affectedRows = await conn.ExecuteAsync(cmd);
        return affectedRows == 1;
    }

    public async Task RevokeAllActiveAsync(
        UserId userId,
        DateTime revokedAtUtc,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           UPDATE refresh_tokens
                           SET revoked_at_utc = @RevokedAtUtc
                           WHERE user_id = @UserId
                             AND revoked_at_utc IS NULL
                             AND expires_at_utc > @RevokedAtUtc
                           """;

        var conn = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(
            sql,
            new
            {
                UserId = userId.Value,
                RevokedAtUtc = revokedAtUtc
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        await conn.ExecuteAsync(cmd);
    }
}
