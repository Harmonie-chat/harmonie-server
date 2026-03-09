using FluentValidation;
using Harmonie.Domain.ValueObjects;

namespace Harmonie.Application.Features.Channels.EditMessage;

public sealed class EditMessageValidator : AbstractValidator<EditMessageRequest>
{
    public EditMessageValidator()
    {
        RuleFor(x => x.Content)
            .NotNull()
            .WithMessage("Message content is required")
            .MaximumLength(MessageContent.MaxLength)
            .WithMessage($"Message content cannot exceed {MessageContent.MaxLength} characters");
    }
}
