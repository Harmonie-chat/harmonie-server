using FluentValidation;

namespace Harmonie.Application.Features.Conversations.EditMessage;

public sealed class EditMessageValidator : AbstractValidator<EditMessageRequest>
{
    public EditMessageValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty()
            .WithMessage("Message content is required");
    }
}
