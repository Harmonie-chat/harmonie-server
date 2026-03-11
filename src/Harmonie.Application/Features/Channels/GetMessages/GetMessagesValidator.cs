using Harmonie.Application.Common;
using FluentValidation;

namespace Harmonie.Application.Features.Channels.GetMessages;

public sealed class GetMessagesValidator : AbstractValidator<GetMessagesRequest>
{
    public GetMessagesValidator()
    {
        RuleFor(x => x.Limit)
            .Must(limit => limit is null || (limit >= 1 && limit <= 100))
            .WithMessage("Limit must be between 1 and 100");

        RuleFor(x => x.Before)
            .Must(before => before is null || MessageCursorCodec.TryParse(before, out _))
            .WithMessage("Before cursor is invalid");
    }
}
