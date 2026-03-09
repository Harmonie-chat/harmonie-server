using Harmonie.Domain.ValueObjects;

namespace Harmonie.Application.Common;

public static class MessageContentErrorCodeResolver
{
    public static string Resolve(string? rawContent)
    {
        if (rawContent is null || rawContent.Trim().Length == 0)
            return ApplicationErrorCodes.Message.ContentEmpty;

        return rawContent.Trim().Length > MessageContent.MaxLength
            ? ApplicationErrorCodes.Message.ContentTooLong
            : ApplicationErrorCodes.Common.DomainRuleViolation;
    }
}
