using FluentValidation;

namespace Harmonie.Application.Features.Conversations.EditDirectMessage;

public sealed class EditDirectMessageValidator : AbstractValidator<EditDirectMessageRequest>
{
    public EditDirectMessageValidator()
    {
        RuleFor(x => x.Content)
            .NotNull()
            .WithMessage("Message content is required")
            .MaximumLength(4000)
            .WithMessage("Message content cannot exceed 4000 characters");
    }
}
