using Harmonie.Domain.Entities.Users;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Uploads;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Interfaces.Users;

public sealed record UserDuplicateCheck(bool EmailExists, bool UsernameExists);

public sealed record SearchUsersQuery(
    string SearchText,
    GuildId? GuildId,
    int Limit);

public sealed record SearchUserResult(
    UserId UserId,
    Username Username,
    string? DisplayName,
    UploadedFileId? AvatarFileId,
    string? AvatarColor,
    string? AvatarIcon,
    string? AvatarBg,
    string? Bio,
    bool IsActive);

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
    /// Get multiple users by their IDs in a single query. Users not found are omitted from the result.
    /// </summary>
    Task<IReadOnlyList<User>> GetManyByIdsAsync(IReadOnlyList<UserId> userIds, CancellationToken cancellationToken = default);

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
    /// Delete a user (soft delete recommended)
    /// </summary>
    Task DeleteAsync(UserId userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the notification context for a user (guilds and conversations they belong to).
    /// </summary>
    Task<UserNotificationContext> GetUserNotificationContextAsync(
        UserId userId,
        CancellationToken cancellationToken = default);
}

public sealed record UserNotificationContext(
    IReadOnlyList<GuildId> GuildIds,
    IReadOnlyList<ConversationId> ConversationIds);
