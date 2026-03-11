using Harmonie.Domain.Entities;
using Harmonie.Domain.ValueObjects;

namespace Harmonie.Application.Interfaces;

public sealed record UserDuplicateCheck(bool EmailExists, bool UsernameExists);

public sealed record SearchUsersQuery(
    string SearchText,
    GuildId? GuildId,
    int Limit);

public sealed record SearchUserResult(
    UserId UserId,
    Username Username,
    string? DisplayName,
    string? AvatarUrl,
    bool IsActive);

public sealed record ProfileUpdateParameters(
    UserId UserId,
    bool DisplayNameIsSet, string? DisplayName,
    bool BioIsSet, string? Bio,
    bool AvatarUrlIsSet, string? AvatarUrl,
    bool AvatarColorIsSet, string? AvatarColor,
    bool AvatarIconIsSet, string? AvatarIcon,
    bool AvatarBgIsSet, string? AvatarBg,
    bool ThemeIsSet, string? Theme,
    bool LanguageIsSet, string? Language,
    DateTime? UpdatedAtUtc);

/// <summary>
/// Repository interface for User aggregate.
/// This is a "port" in Hexagonal Architecture - the infrastructure layer provides the implementation.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Get a user by their ID
    /// </summary>
    Task<User?> GetByIdAsync(UserId userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a user by their email address
    /// </summary>
    Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a user by their username
    /// </summary>
    Task<User?> GetByUsernameAsync(Username username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if an email already exists
    /// </summary>
    Task<bool> ExistsByEmailAsync(Email email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a username already exists
    /// </summary>
    Task<bool> ExistsByUsernameAsync(Username username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if an active user references the provided avatar URL.
    /// </summary>
    Task<bool> ExistsByAvatarUrlAsync(string avatarUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check email and username uniqueness in a single query
    /// </summary>
    Task<UserDuplicateCheck> CheckDuplicatesAsync(Email email, Username username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Search active users by username or display name, optionally scoped to a guild.
    /// </summary>
    Task<IReadOnlyList<SearchUserResult>> SearchUsersAsync(
        SearchUsersQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Add a new user
    /// </summary>
    Task AddAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an existing user
    /// </summary>
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update only profile fields for an existing user.
    /// </summary>
    Task UpdateProfileAsync(
        ProfileUpdateParameters parameters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a user (soft delete recommended)
    /// </summary>
    Task DeleteAsync(UserId userId, CancellationToken cancellationToken = default);
}
