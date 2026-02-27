using Harmonie.Domain.ValueObjects;

namespace Harmonie.Application.Interfaces;

/// <summary>
/// Repository contract for refresh token persistence and rotation.
/// </summary>
public interface IRefreshTokenRepository
{
    /// <summary>
    /// Persist a refresh token hash for a user.
    /// </summary>
    Task StoreAsync(
        UserId userId,
        string tokenHash,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a refresh token session by token hash.
    /// </summary>
    Task<RefreshTokenSession?> GetByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revoke an existing token and atomically create the replacement token.
    /// </summary>
    Task<bool> RotateAsync(
        Guid tokenId,
        UserId userId,
        string newTokenHash,
        DateTime newExpiresAtUtc,
        DateTime revokedAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revoke a refresh token for a specific user/session when still active.
    /// </summary>
    Task<bool> RevokeActiveAsync(
        UserId userId,
        string tokenHash,
        DateTime revokedAtUtc,
        string revocationReason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revoke all active refresh token sessions for a specific user.
    /// </summary>
    Task RevokeAllActiveAsync(
        UserId userId,
        DateTime revokedAtUtc,
        string revocationReason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revoke all active sessions in a refresh-token family starting from a reused token.
    /// </summary>
    Task RevokeActiveFamilyAsync(
        Guid tokenId,
        DateTime revokedAtUtc,
        string revocationReason,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Refresh token database session shape used by Application layer.
/// </summary>
public sealed record RefreshTokenSession(
    Guid Id,
    UserId UserId,
    DateTime ExpiresAtUtc,
    DateTime? RevokedAtUtc,
    string? RevocationReason,
    Guid? ReplacedByTokenId);
