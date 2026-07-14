namespace Harmonie.Application.Common.Auth;

public static class TokenLifetimeValidator
{
    public static bool IsValid(DateTime? notBeforeUtc, DateTime? expiresAtUtc, DateTime nowUtc)
    {
        return expiresAtUtc is not null
            && expiresAtUtc.Value > nowUtc
            && (notBeforeUtc is null || notBeforeUtc.Value <= nowUtc);
    }
}
