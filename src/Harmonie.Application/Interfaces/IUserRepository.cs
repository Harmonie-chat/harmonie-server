using Harmonie.Domain.Entities;
using Harmonie.Domain.ValueObjects;

namespace Harmonie.Application.Interfaces;

public sealed record UserDuplicateCheck(bool EmailExists, bool UsernameExists);

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
    /// Check email and username uniqueness in a single query
    /// </summary>
    Task<UserDuplicateCheck> CheckDuplicatesAsync(Email email, Username username, CancellationToken cancellationToken = default);

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
        UserId userId,
        bool displayNameIsSet,
        string? displayName,
        bool bioIsSet,
        string? bio,
        bool avatarUrlIsSet,
        string? avatarUrl,
        DateTime? updatedAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a user (soft delete recommended)
    /// </summary>
    Task DeleteAsync(UserId userId, CancellationToken cancellationToken = default);
}
