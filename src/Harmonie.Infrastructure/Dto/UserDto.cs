namespace Harmonie.Infrastructure.Dto;

public sealed record UserDto(
    Guid Id,
    string Email,
    string Username,
    string PasswordHash,
    string? AvatarUrl,
    bool IsEmailVerified,
    bool IsActive,
    string? DisplayName,
    string? Bio,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    DateTime? LastLoginAtUtc,
    DateTime? DeletedAt);