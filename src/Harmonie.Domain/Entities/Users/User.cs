using Harmonie.Domain.Common;
using Harmonie.Domain.ValueObjects.Common;
using Harmonie.Domain.ValueObjects.Uploads;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Domain.Entities.Users;

/// <summary>
/// User aggregate root.
/// Represents a user account across all Harmonie server instances.
/// </summary>
public sealed class User : Entity<UserId>
{
    /// <summary>
    /// Email address (unique identifier for authentication)
    /// </summary>
    public Email Email { get; private set; }

    /// <summary>
    /// Display username (unique within instance)
    /// </summary>
    public Username Username { get; private set; }

    /// <summary>
    /// Hashed password (BCrypt/PBKDF2)
    /// </summary>
    public string PasswordHash { get; private set; }

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
    /// Avatar visual appearance (color, icon, background).
    /// </summary>
    public Appearance Avatar { get; private set; } = Appearance.Empty;

    /// <summary>
    /// UI theme preference. Themes are open-ended (user-supplied strings);
    /// validated for length only (max 50 chars, non-empty).
    /// </summary>
    public string Theme { get; private set; }

    /// <summary>
    /// Language preference (ISO 639-1)
    /// </summary>
    public string? Language { get; private set; }

    /// <summary>
    /// User presence status (online, idle, dnd, invisible).
    /// Enforced by <see cref="UserStatus"/> at every entry point.
    /// </summary>
    public UserStatus Status { get; private set; }

    /// <summary>
    /// When the status was last updated (UTC)
    /// </summary>
    public DateTime? StatusUpdatedAtUtc { get; private set; }

    private User(
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
        Appearance avatar,
        string theme,
        string? language,
        UserStatus status,
        DateTime? statusUpdatedAtUtc,
        DateTime createdAtUtc,
        DateTime? updatedAtUtc)
    {
        Id = id;
        Email = email;
        Username = username;
        PasswordHash = passwordHash;
        AvatarFileId = avatarFileId;
        IsEmailVerified = isEmailVerified;
        IsActive = isActive;
        LastLoginAtUtc = lastLoginAtUtc;
        DisplayName = displayName;
        Bio = bio;
        Avatar = avatar;
        Theme = theme;
        Language = language;
        Status = status;
        StatusUpdatedAtUtc = statusUpdatedAtUtc;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    /// <summary>
    /// Create a new user account.
    /// Factory method enforcing business rules.
    /// </summary>
    public static Result<User> Create(
        Email email,
        Username username,
        string passwordHash,
        DateTime createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            return Result.Failure<User>("Password hash cannot be empty");

        var user = new User(
            UserId.New(),
            email,
            username,
            passwordHash,
            avatarFileId: null,
            isEmailVerified: false,
            isActive: true,
            lastLoginAtUtc: null,
            displayName: null,
            bio: null,
            avatar: Appearance.Empty,
            theme: "default",
            language: null,
            status: UserStatus.Online,
            statusUpdatedAtUtc: null,
            createdAtUtc,
            updatedAtUtc: createdAtUtc);

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
        Appearance avatar,
        string theme,
        string? language,
        UserStatus status,
        DateTime? statusUpdatedAtUtc,
        DateTime createdAtUtc,
        DateTime? updatedAtUtc)
    {
        return new User(
            id,
            email,
            username,
            passwordHash,
            avatarFileId,
            isEmailVerified,
            isActive,
            lastLoginAtUtc,
            displayName,
            bio,
            avatar,
            theme,
            language,
            status,
            statusUpdatedAtUtc,
            createdAtUtc,
            updatedAtUtc);
    }

    /// <summary>
    /// Update the user's email address
    /// </summary>
    public Result UpdateEmail(Email newEmail, DateTime updatedAtUtc)
    {
        if (Email == newEmail)
            return Result.Success();

        var oldEmail = Email;
        Email = newEmail;
        IsEmailVerified = false; // Require re-verification
        MarkAsUpdated(updatedAtUtc);

        return Result.Success();
    }

    /// <summary>
    /// Update the user's username
    /// </summary>
    public Result UpdateUsername(Username newUsername, DateTime updatedAtUtc)
    {
        if (Username == newUsername)
            return Result.Success();

        var oldUsername = Username;
        Username = newUsername;
        MarkAsUpdated(updatedAtUtc);

        return Result.Success();
    }

    /// <summary>
    /// Update the user's password hash
    /// </summary>
    public Result UpdatePassword(string newPasswordHash, DateTime updatedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
            return Result.Failure("Password hash cannot be empty");

        PasswordHash = newPasswordHash;
        MarkAsUpdated(updatedAtUtc);

        return Result.Success();
    }

    /// <summary>
    /// Update the user's avatar file
    /// </summary>
    public Result UpdateAvatarFile(UploadedFileId? avatarFileId, DateTime updatedAtUtc)
    {
        AvatarFileId = avatarFileId;
        MarkAsUpdated(updatedAtUtc);

        return Result.Success();
    }

    /// <summary>
    /// Update the user's display name
    /// </summary>
    public Result UpdateDisplayName(string? displayName, DateTime updatedAtUtc)
    {
        if (displayName?.Length > 100)
            return Result.Failure("Display name is too long");

        DisplayName = displayName;
        MarkAsUpdated(updatedAtUtc);

        return Result.Success();
    }

    /// <summary>
    /// Update the user's bio
    /// </summary>
    public Result UpdateBio(string? bio, DateTime updatedAtUtc)
    {
        if (bio?.Length > 500)
            return Result.Failure("Bio is too long");

        Bio = bio;
        MarkAsUpdated(updatedAtUtc);

        return Result.Success();
    }

    /// <summary>
    /// Update the user's avatar appearance (color, icon, background).
    /// </summary>
    public Result UpdateAvatar(Appearance appearance, DateTime updatedAtUtc)
    {
        Avatar = appearance;
        MarkAsUpdated(updatedAtUtc);
        return Result.Success();
    }

    public Result UpdateTheme(string theme, DateTime updatedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(theme))
            return Result.Failure("Theme cannot be empty");

        if (theme.Length > 50)
            return Result.Failure("Theme is too long");

        Theme = theme;
        MarkAsUpdated(updatedAtUtc);
        return Result.Success();
    }

    public Result UpdateLanguage(string? language, DateTime updatedAtUtc)
    {
        if (language?.Length > 10)
            return Result.Failure("Language is too long");

        Language = language;
        MarkAsUpdated(updatedAtUtc);
        return Result.Success();
    }

    public Result UpdateStatus(UserStatus status, DateTime updatedAtUtc)
    {
        if (Status == status)
            return Result.Success();

        Status = status;
        StatusUpdatedAtUtc = updatedAtUtc;
        MarkAsUpdated(updatedAtUtc);
        return Result.Success();
    }

    /// <summary>
    /// Verify the user's email address
    /// </summary>
    public Result VerifyEmail(DateTime updatedAtUtc)
    {
        if (IsEmailVerified)
            return Result.Success();

        IsEmailVerified = true;
        MarkAsUpdated(updatedAtUtc);

        return Result.Success();
    }

    /// <summary>
    /// Deactivate the user account
    /// </summary>
    public Result Deactivate(DateTime updatedAtUtc)
    {
        if (!IsActive)
            return Result.Failure("User is already deactivated");

        IsActive = false;
        MarkAsUpdated(updatedAtUtc);

        return Result.Success();
    }

    public Result Reactivate(DateTime updatedAtUtc)
    {
        if (IsActive)
            return Result.Failure("User is already active");

        IsActive = true;
        MarkAsUpdated(updatedAtUtc);

        return Result.Success();
    }

    /// <summary>
    /// Record a successful login
    /// </summary>
    public void RecordLogin(DateTime loginAtUtc)
    {
        LastLoginAtUtc = loginAtUtc;
        MarkAsUpdated(loginAtUtc);
    }
}
