namespace Harmonie.Infrastructure.Dto;

public sealed record RefreshTokenDto(
    Guid Id,
    Guid UserId,
    string TokenHash,
    DateTime ExpiresAtUtc,
    DateTime? RevokedAtUtc,
    string? RevocationReason,
    Guid? ReplacedByTokenId);
