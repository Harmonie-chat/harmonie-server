using System.Globalization;
using FluentValidation;
using Harmonie.Application.Common;

namespace Harmonie.Application.Features.Conversations.SearchConversationMessages;

public sealed class SearchConversationMessagesValidator : AbstractValidator<SearchConversationMessagesRequest>
{
    public SearchConversationMessagesValidator()
    {
        RuleFor(x => x.Q)
            .NotEmpty()
            .WithMessage("Search query is required");

        RuleFor(x => x.Before)
            .Must(before => before is null || TryParseUtcDateTime(before, out _))
            .WithMessage("Before must be a valid ISO 8601 date/time");

        RuleFor(x => x.After)
            .Must(after => after is null || TryParseUtcDateTime(after, out _))
            .WithMessage("After must be a valid ISO 8601 date/time");

        RuleFor(x => x)
            .Must(HaveValidDateRange)
            .WithMessage("After must be earlier than or equal to before when both are provided");

        RuleFor(x => x.Cursor)
            .Must(cursor => cursor is null || MessageCursorCodec.TryParse(cursor, out _))
            .WithMessage("Cursor is invalid");

        RuleFor(x => x.Limit)
            .Must(limit => limit is null || (limit >= 1 && limit <= 100))
            .WithMessage("Limit must be between 1 and 100");
    }

    private static bool HaveValidDateRange(SearchConversationMessagesRequest request)
    {
        if (request.After is null || request.Before is null)
            return true;

        return TryParseUtcDateTime(request.After, out var after)
            && TryParseUtcDateTime(request.Before, out var before)
            && after <= before;
    }

    private static bool TryParseUtcDateTime(string input, out DateTime value)
    {
        value = default;

        if (!DateTimeOffset.TryParse(
                input,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            return false;
        }

        value = parsed.UtcDateTime;
        return true;
    }
}
