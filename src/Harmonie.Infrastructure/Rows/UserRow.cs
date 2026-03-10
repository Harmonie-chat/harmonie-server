namespace Harmonie.Infrastructure.Rows;

public sealed record UserRow(
    Guid Id,
    string Email,
    string Username,
    string PasswordHash,
    string? AvatarUrl,
    bool IsEmailVerified,
    bool IsActive,
    string? DisplayName,
    string? Bio,
    string? AvatarColor,
    string? AvatarIcon,
    string? AvatarBg,
    string Theme,
    string? Language,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    DateTime? LastLoginAtUtc,
    DateTime? DeletedAt);
