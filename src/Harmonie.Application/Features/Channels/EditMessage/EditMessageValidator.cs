using FluentValidation;

namespace Harmonie.Application.Features.Channels.EditMessage;

public sealed class EditMessageValidator : AbstractValidator<EditMessageRequest>
{
    public EditMessageValidator()
    {
        RuleFor(x => x.Content)
            .NotNull()
            .WithMessage("Message content is required")
            .MaximumLength(4000)
            .WithMessage("Message content cannot exceed 4000 characters");
    }
}
