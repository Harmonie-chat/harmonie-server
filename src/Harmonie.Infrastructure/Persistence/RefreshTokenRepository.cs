using Dapper;
using Harmonie.Application.Common;
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
            INSERT INTO refresh_tokens (
                id,
                user_id,
                token_hash,
                created_at_utc,
                expires_at_utc,
                revocation_reason,
                replaced_by_token_id)
            VALUES (
                @Id,
                @UserId,
                @TokenHash,
                @CreatedAtUtc,
                @ExpiresAtUtc,
                NULL,
                NULL)";

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
                   revoked_at_utc as ""RevokedAtUtc"",
                   revocation_reason as ""RevocationReason"",
                   replaced_by_token_id as ""ReplacedByTokenId""
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
            RevokedAtUtc: tokenRow.RevokedAtUtc,
            RevocationReason: tokenRow.RevocationReason,
            ReplacedByTokenId: tokenRow.ReplacedByTokenId);
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
        var newTokenId = Guid.NewGuid();

        const string revokeSql = """
                                 UPDATE refresh_tokens
                                 SET revoked_at_utc = @RevokedAtUtc,
                                     revocation_reason = @RevocationReason
                                 WHERE id = @Id
                                   AND user_id = @UserId
                                   AND revoked_at_utc IS NULL
                                   AND expires_at_utc > @RevokedAtUtc
                                 """;

        const string insertSql = """
                                 INSERT INTO refresh_tokens (
                                     id,
                                     user_id,
                                     token_hash,
                                     created_at_utc,
                                     expires_at_utc,
                                     revocation_reason,
                                     replaced_by_token_id)
                                 VALUES (
                                     @Id,
                                     @UserId,
                                     @TokenHash,
                                     @CreatedAtUtc,
                                     @ExpiresAtUtc,
                                     NULL,
                                     NULL)
                                 """;

        const string linkReplacementSql = """
                                          UPDATE refresh_tokens
                                          SET replaced_by_token_id = @ReplacedByTokenId
                                          WHERE id = @Id
                                          """;

        var revokeCmd = new CommandDefinition(
            revokeSql,
            new
            {
                Id = tokenId,
                UserId = userId.Value,
                RevokedAtUtc = revokedAtUtc,
                RevocationReason = RefreshTokenRevocationReasons.Rotated
            },
            transaction: tx,
            cancellationToken: cancellationToken);

        var affectedRows = await conn.ExecuteAsync(revokeCmd);
        if (affectedRows != 1)
            return false;

        var insertCmd = new CommandDefinition(
            insertSql,
            new
            {
                Id = newTokenId,
                UserId = userId.Value,
                TokenHash = newTokenHash,
                CreatedAtUtc = revokedAtUtc,
                ExpiresAtUtc = newExpiresAtUtc
            },
            transaction: tx,
            cancellationToken: cancellationToken);

        await conn.ExecuteAsync(insertCmd);

        var linkCmd = new CommandDefinition(
            linkReplacementSql,
            new
            {
                Id = tokenId,
                ReplacedByTokenId = newTokenId
            },
            transaction: tx,
            cancellationToken: cancellationToken);

        await conn.ExecuteAsync(linkCmd);
        return true;
    }

    public async Task<bool> RevokeActiveAsync(
        UserId userId,
        string tokenHash,
        DateTime revokedAtUtc,
        string revocationReason,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           UPDATE refresh_tokens
                           SET revoked_at_utc = @RevokedAtUtc,
                               revocation_reason = @RevocationReason
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
                RevokedAtUtc = revokedAtUtc,
                RevocationReason = revocationReason
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        var affectedRows = await conn.ExecuteAsync(cmd);
        return affectedRows == 1;
    }

    public async Task RevokeAllActiveAsync(
        UserId userId,
        DateTime revokedAtUtc,
        string revocationReason,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           UPDATE refresh_tokens
                           SET revoked_at_utc = @RevokedAtUtc,
                               revocation_reason = @RevocationReason
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
                RevokedAtUtc = revokedAtUtc,
                RevocationReason = revocationReason
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        await conn.ExecuteAsync(cmd);
    }

    public async Task RevokeActiveFamilyAsync(
        Guid tokenId,
        DateTime revokedAtUtc,
        string revocationReason,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           WITH RECURSIVE token_family AS (
                               SELECT id, replaced_by_token_id
                               FROM refresh_tokens
                               WHERE id = @TokenId

                               UNION ALL

                               SELECT next_token.id, next_token.replaced_by_token_id
                               FROM refresh_tokens next_token
                               INNER JOIN token_family family
                                   ON next_token.id = family.replaced_by_token_id
                           )
                           UPDATE refresh_tokens
                           SET revoked_at_utc = @RevokedAtUtc,
                               revocation_reason = @RevocationReason
                           WHERE id IN (SELECT id FROM token_family)
                             AND revoked_at_utc IS NULL
                             AND expires_at_utc > @RevokedAtUtc
                           """;

        var conn = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(
            sql,
            new
            {
                TokenId = tokenId,
                RevokedAtUtc = revokedAtUtc,
                RevocationReason = revocationReason
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        await conn.ExecuteAsync(cmd);
    }
}
