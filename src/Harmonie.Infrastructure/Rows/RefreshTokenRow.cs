namespace Harmonie.Infrastructure.Rows;

public sealed record RefreshTokenRow(
    Guid Id,
    Guid UserId,
    string TokenHash,
    DateTime ExpiresAtUtc,
    DateTime? RevokedAtUtc,
    string? RevocationReason,
    Guid? ReplacedByTokenId);
