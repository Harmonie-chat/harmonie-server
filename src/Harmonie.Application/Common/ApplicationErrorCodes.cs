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

    public static class Auth
    {
        public const string InvalidCredentials = "AUTH_INVALID_CREDENTIALS";
        public const string InvalidRefreshToken = "AUTH_INVALID_REFRESH_TOKEN";
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
    }

    public static class Channel
    {
        public const string NotFound = "CHANNEL_NOT_FOUND";
        public const string NotText = "CHANNEL_NOT_TEXT";
        public const string AccessDenied = "CHANNEL_ACCESS_DENIED";
    }

    public static class Message
    {
        public const string ContentEmpty = "MESSAGE_CONTENT_EMPTY";
        public const string ContentTooLong = "MESSAGE_CONTENT_TOO_LONG";
    }
}
