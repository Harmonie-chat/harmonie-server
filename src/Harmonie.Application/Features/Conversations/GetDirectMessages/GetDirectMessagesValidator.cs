using Harmonie.Application.Common;
using FluentValidation;

namespace Harmonie.Application.Features.Conversations.GetDirectMessages;

public sealed class GetDirectMessagesValidator : AbstractValidator<GetDirectMessagesRequest>
{
    public GetDirectMessagesValidator()
    {
        RuleFor(x => x.Limit)
            .Must(limit => limit is null || (limit >= 1 && limit <= 100))
            .WithMessage("Limit must be between 1 and 100");

        RuleFor(x => x.Cursor)
            .Must(cursor => cursor is null || MessageCursorCodec.TryParse(cursor, out _))
            .WithMessage("Cursor is invalid");
    }
}
