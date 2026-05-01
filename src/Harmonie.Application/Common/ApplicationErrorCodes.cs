namespace Harmonie.Application.Common;

/// <summary>
/// Catalog of stable, endpoint-wide application error codes.
/// </summary>
public static class ApplicationErrorCodes
{
    public static class Common
    {
        public const string ValidationFailed = "COMMON_VALIDATION_FAILED";
        public const string DomainRuleViolation = "COMMON_DOMAIN_RULE_VIOLATION";
        public const string InvalidState = "COMMON_INVALID_STATE";
        public const string Unexpected = "COMMON_UNEXPECTED";
    }

    public static class Validation
    {
        public const string Required = "VALIDATION_REQUIRED";
        public const string Invalid = "VALIDATION_INVALID";
        public const string InvalidFormat = "VALIDATION_INVALID_FORMAT";
        public const string Email = "VALIDATION_EMAIL";
        public const string MinLength = "VALIDATION_MIN_LENGTH";
        public const string MaxLength = "VALIDATION_MAX_LENGTH";
        public const string OutOfRange = "VALIDATION_OUT_OF_RANGE";
        public const string WrongEnumValue = "VALIDATION_WRONG_ENUM_VALUE";
    }

    public static class Auth
    {
        public const string InvalidCredentials = "AUTH_INVALID_CREDENTIALS";
        public const string InvalidRefreshToken = "AUTH_INVALID_REFRESH_TOKEN";
        public const string RefreshTokenReuseDetected = "AUTH_REFRESH_TOKEN_REUSE_DETECTED";
        public const string UserInactive = "AUTH_USER_INACTIVE";
        public const string DuplicateEmail = "AUTH_DUPLICATE_EMAIL";
        public const string DuplicateUsername = "AUTH_DUPLICATE_USERNAME";
    }

    public static class Guild
    {
        public const string NotFound = "GUILD_NOT_FOUND";
        public const string AccessDenied = "GUILD_ACCESS_DENIED";
        public const string InviteForbidden = "GUILD_INVITE_FORBIDDEN";
        public const string InviteTargetNotFound = "GUILD_INVITE_TARGET_NOT_FOUND";
        public const string MemberAlreadyExists = "GUILD_MEMBER_ALREADY_EXISTS";
        public const string NameConflict = "GUILD_NAME_CONFLICT";
        public const string OwnerCannotLeave = "GUILD_OWNER_CANNOT_LEAVE";
        public const string MemberNotFound = "GUILD_MEMBER_NOT_FOUND";
        public const string OwnerCannotBeRemoved = "GUILD_OWNER_CANNOT_BE_REMOVED";
        public const string OwnerRoleCannotBeChanged = "GUILD_OWNER_ROLE_CANNOT_BE_CHANGED";
        public const string OwnerTransferToSelf = "GUILD_OWNER_TRANSFER_TO_SELF";
        public const string CannotBanSelf = "GUILD_CANNOT_BAN_SELF";
        public const string OwnerCannotBeBanned = "GUILD_OWNER_CANNOT_BE_BANNED";
        public const string AlreadyBanned = "GUILD_ALREADY_BANNED";
        public const string UserBanned = "GUILD_USER_BANNED";
        public const string NotBanned = "GUILD_NOT_BANNED";
    }

    public static class Channel
    {
        public const string NotFound = "CHANNEL_NOT_FOUND";
        public const string NotText = "CHANNEL_NOT_TEXT";
        public const string NotVoice = "CHANNEL_NOT_VOICE";
        public const string AccessDenied = "CHANNEL_ACCESS_DENIED";
        public const string NameConflict = "CHANNEL_NAME_CONFLICT";
        public const string CannotDeleteDefault = "CHANNEL_CANNOT_DELETE_DEFAULT";
    }

    public static class Message
    {
        public const string ContentEmpty = "MESSAGE_CONTENT_EMPTY";
        public const string ContentTooLong = "MESSAGE_CONTENT_TOO_LONG";
        public const string NotFound = "MESSAGE_NOT_FOUND";
        public const string AttachmentNotFound = "MESSAGE_ATTACHMENT_NOT_FOUND";
        public const string EditForbidden = "MESSAGE_EDIT_FORBIDDEN";
        public const string DeleteForbidden = "MESSAGE_DELETE_FORBIDDEN";
    }

    public static class Reaction
    {
        public const string MessageNotFound = "REACTION_MESSAGE_NOT_FOUND";
    }

    public static class User
    {
        public const string NotFound = "USER_NOT_FOUND";
    }

    public static class Upload
    {
        public const string StorageUnavailable = "UPLOAD_STORAGE_UNAVAILABLE";
        public const string NotFound = "UPLOAD_NOT_FOUND";
        public const string AccessDenied = "UPLOAD_ACCESS_DENIED";
    }

    public static class Invite
    {
        public const string NotFound = "INVITE_NOT_FOUND";
        public const string Expired = "INVITE_EXPIRED";
        public const string Exhausted = "INVITE_EXHAUSTED";
        public const string RevokeForbidden = "INVITE_REVOKE_FORBIDDEN";
    }

    public static class Conversation
    {
        public const string NotFound = "CONVERSATION_NOT_FOUND";
        public const string CannotOpenSelf = "CONVERSATION_CANNOT_OPEN_SELF";
        public const string AccessDenied = "CONVERSATION_ACCESS_DENIED";
        public const string InvalidConversationType = "CONVERSATION_INVALID_TYPE";
    }
}
