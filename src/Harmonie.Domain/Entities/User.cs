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

    public UploadedFileId? AvatarFileId { get; private set; }

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

    /// <summary>
    /// Avatar appearance color
    /// </summary>
    public string? AvatarColor { get; private set; }

    /// <summary>
    /// Avatar appearance icon
    /// </summary>
    public string? AvatarIcon { get; private set; }

    /// <summary>
    /// Avatar appearance background
    /// </summary>
    public string? AvatarBg { get; private set; }

    /// <summary>
    /// UI theme preference
    /// </summary>
    public string Theme { get; private set; } = "default";

    /// <summary>
    /// Language preference (ISO 639-1)
    /// </summary>
    public string? Language { get; private set; }

    /// <summary>
    /// User presence status (online, idle, dnd, invisible)
    /// </summary>
    public string Status { get; private set; } = "online";

    /// <summary>
    /// When the status was last updated (UTC)
    /// </summary>
    public DateTime? StatusUpdatedAtUtc { get; private set; }

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
        UploadedFileId? avatarFileId,
        bool isEmailVerified,
        bool isActive,
        DateTime? lastLoginAtUtc,
        string? displayName,
        string? bio,
        string? avatarColor,
        string? avatarIcon,
        string? avatarBg,
        string theme,
        string? language,
        string status,
        DateTime? statusUpdatedAtUtc,
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
            AvatarFileId = avatarFileId,
            IsEmailVerified = isEmailVerified,
            IsActive = isActive,
            LastLoginAtUtc = lastLoginAtUtc,
            DisplayName = displayName,
            Bio = bio,
            AvatarColor = avatarColor,
            AvatarIcon = avatarIcon,
            AvatarBg = avatarBg,
            Theme = theme,
            Language = language,
            Status = status,
            StatusUpdatedAtUtc = statusUpdatedAtUtc,
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
    /// Update the user's avatar file
    /// </summary>
    public Result UpdateAvatarFile(UploadedFileId? avatarFileId)
    {
        AvatarFileId = avatarFileId;
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

    public Result UpdateAvatarColor(string? avatarColor)
    {
        if (avatarColor?.Length > 50)
            return Result.Failure("Avatar color is too long");

        AvatarColor = avatarColor;
        MarkAsUpdated();
        return Result.Success();
    }

    public Result UpdateAvatarIcon(string? avatarIcon)
    {
        if (avatarIcon?.Length > 50)
            return Result.Failure("Avatar icon is too long");

        AvatarIcon = avatarIcon;
        MarkAsUpdated();
        return Result.Success();
    }

    public Result UpdateAvatarBg(string? avatarBg)
    {
        if (avatarBg?.Length > 50)
            return Result.Failure("Avatar background is too long");

        AvatarBg = avatarBg;
        MarkAsUpdated();
        return Result.Success();
    }

    public Result UpdateTheme(string theme)
    {
        if (string.IsNullOrWhiteSpace(theme))
            return Result.Failure("Theme cannot be empty");

        if (theme.Length > 50)
            return Result.Failure("Theme is too long");

        Theme = theme;
        MarkAsUpdated();
        return Result.Success();
    }

    public Result UpdateLanguage(string? language)
    {
        if (language?.Length > 10)
            return Result.Failure("Language is too long");

        Language = language;
        MarkAsUpdated();
        return Result.Success();
    }

    private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "online", "idle", "dnd", "invisible"
    };

    public Result UpdateStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return Result.Failure("Status cannot be empty");

        if (!ValidStatuses.Contains(status))
            return Result.Failure("Status must be one of: online, idle, dnd, invisible");

        Status = status.ToLowerInvariant();
        StatusUpdatedAtUtc = DateTime.UtcNow;
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
