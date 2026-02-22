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
}
