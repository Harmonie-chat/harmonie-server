using FluentValidation;
using Harmonie.Domain.ValueObjects;

namespace Harmonie.Application.Features.Conversations.EditDirectMessage;

public sealed class EditDirectMessageValidator : AbstractValidator<EditDirectMessageRequest>
{
    public EditDirectMessageValidator()
    {
        RuleFor(x => x.Content)
            .NotNull()
            .WithMessage("Message content is required")
            .MaximumLength(MessageContent.MaxLength)
            .WithMessage($"Message content cannot exceed {MessageContent.MaxLength} characters");
    }
}
