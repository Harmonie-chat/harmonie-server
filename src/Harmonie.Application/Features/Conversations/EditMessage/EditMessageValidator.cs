using FluentValidation;

namespace Harmonie.Application.Features.Conversations.EditMessage;

public sealed class EditMessageValidator : AbstractValidator<EditMessageRequest>
{
    public EditMessageValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty()
            .WithMessage("Message content is required");

        RuleForEach(x => x.MentionedUserIds)
            .NotEqual(Guid.Empty)
            .WithMessage("Mentioned user IDs must be valid non-empty GUIDs")
            .When(x => x.MentionedUserIds is not null);

        RuleFor(x => x.MentionedUserIds)
            .Must(ids => ids is null || ids.Distinct().Count() == ids.Count)
            .WithMessage("Mentioned user IDs must be unique")
            .When(x => x.MentionedUserIds is not null);

        RuleFor(x => x.MentionedUserIds)
            .Must(ids => ids is null || ids.Count <= 50)
            .WithMessage("A message can mention at most 50 users")
            .When(x => x.MentionedUserIds is not null);
    }
}
