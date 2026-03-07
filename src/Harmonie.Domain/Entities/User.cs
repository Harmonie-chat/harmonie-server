using Harmonie.Domain.Common;
using Harmonie.Domain.ValueObjects;

namespace Harmonie.Domain.Entities;

/// <summary>
/// User aggregate root.
/// Represents a user account across all Harmonie server instances.
/// </summary>
public sealed class User : Entity<UserId>
{
    /// <summary>
    /// Email address (unique identifier for authentication)
    /// </summary>
    public Email Email { get; private set; } = null!;

    /// <summary>
    /// Display username (unique within instance)
    /// </summary>
    public Username Username { get; private set; } = null!;

    /// <summary>
    /// Hashed password (BCrypt/PBKDF2)
    /// </summary>
    public string PasswordHash { get; private set; } = null!;

    /// <summary>
    /// URL to user's avatar image
    /// </summary>
    public string? AvatarUrl { get; private set; }

    /// <summary>
    /// Whether the user's email has been verified
    /// </summary>
    public bool IsEmailVerified { get; private set; }

    /// <summary>
    /// Whether the user account is active
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// When the user last logged in (UTC)
    /// </summary>
    public DateTime? LastLoginAtUtc { get; private set; }

    /// <summary>
    /// User's preferred display name (optional, different from username)
    /// </summary>
    public string? DisplayName { get; private set; }

    /// <summary>
    /// User's bio/about section
    /// </summary>
    public string? Bio { get; private set; }

    // Private constructor for EF Core / Dapper
    private User() { }

    /// <summary>
    /// Create a new user account.
    /// Factory method enforcing business rules.
    /// </summary>
    public static Result<User> Create(
        Email email,
        Username username,
        string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            return Result.Failure<User>("Password hash cannot be empty");

        var user = new User
        {
            Id = UserId.New(),
            Email = email,
            Username = username,
            PasswordHash = passwordHash,
            IsEmailVerified = false,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        return Result.Success(user);
    }
    
    public static User Rehydrate(
        UserId id,
        Email email,
        Username username,
        string passwordHash,
        string? avatarUrl,
        bool isEmailVerified,
        bool isActive,
        DateTime? lastLoginAtUtc,
        string? displayName,
        string? bio,
        DateTime createdAtUtc,
        DateTime? updatedAtUtc
    )
    {
        return new User
        {
            Id = id,
            Email = email,
            Username = username,
            PasswordHash = passwordHash,
            AvatarUrl = avatarUrl,
            IsEmailVerified = isEmailVerified,
            IsActive = isActive,
            LastLoginAtUtc = lastLoginAtUtc,
            DisplayName = displayName,
            Bio = bio,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = updatedAtUtc
        };
    }

    /// <summary>
    /// Update the user's email address
    /// </summary>
    public Result UpdateEmail(Email newEmail)
    {
        if (Email == newEmail)
            return Result.Success();

        var oldEmail = Email;
        Email = newEmail;
        IsEmailVerified = false; // Require re-verification
        MarkAsUpdated();

        return Result.Success();
    }

    /// <summary>
    /// Update the user's username
    /// </summary>
    public Result UpdateUsername(Username newUsername)
    {
        if (Username == newUsername)
            return Result.Success();

        var oldUsername = Username;
        Username = newUsername;
        MarkAsUpdated();

        return Result.Success();
    }

    /// <summary>
    /// Update the user's password hash
    /// </summary>
    public Result UpdatePassword(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
            return Result.Failure("Password hash cannot be empty");

        PasswordHash = newPasswordHash;
        MarkAsUpdated();

        return Result.Success();
    }

    /// <summary>
    /// Update the user's avatar
    /// </summary>
    public Result UpdateAvatar(string? avatarUrl)
    {
        if (avatarUrl?.Length > 2048)
            return Result.Failure("Avatar URL is too long");

        AvatarUrl = avatarUrl;
        MarkAsUpdated();

        return Result.Success();
    }

    /// <summary>
    /// Update the user's display name
    /// </summary>
    public Result UpdateDisplayName(string? displayName)
    {
        if (displayName?.Length > 100)
            return Result.Failure("Display name is too long");

        DisplayName = displayName;
        MarkAsUpdated();

        return Result.Success();
    }

    /// <summary>
    /// Update the user's bio
    /// </summary>
    public Result UpdateBio(string? bio)
    {
        if (bio?.Length > 500)
            return Result.Failure("Bio is too long");

        Bio = bio;
        MarkAsUpdated();

        return Result.Success();
    }

    /// <summary>
    /// Verify the user's email address
    /// </summary>
    public Result VerifyEmail()
    {
        if (IsEmailVerified)
            return Result.Success();

        IsEmailVerified = true;
        MarkAsUpdated();

        return Result.Success();
    }

    /// <summary>
    /// Deactivate the user account
    /// </summary>
    public Result Deactivate()
    {
        if (!IsActive)
            return Result.Failure("User is already deactivated");

        IsActive = false;
        MarkAsUpdated();

        return Result.Success();
    }

    public Result Reactivate()
    {
        if (IsActive)
            return Result.Failure("User is already active");

        IsActive = true;
        MarkAsUpdated();

        return Result.Success();
    }

    /// <summary>
    /// Record a successful login
    /// </summary>
    public void RecordLogin()
    {
        LastLoginAtUtc = DateTime.UtcNow;
        MarkAsUpdated();
    }
}
