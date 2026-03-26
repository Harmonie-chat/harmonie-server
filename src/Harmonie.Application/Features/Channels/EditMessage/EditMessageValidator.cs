using FluentValidation;

namespace Harmonie.Application.Features.Channels.EditMessage;

public sealed class EditMessageValidator : AbstractValidator<EditMessageRequest>
{
    public EditMessageValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty()
            .WithMessage("Message content is required");
    }
}
