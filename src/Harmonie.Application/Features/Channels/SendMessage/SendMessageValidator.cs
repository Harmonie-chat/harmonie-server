using FluentValidation;
using Harmonie.Domain.ValueObjects;

namespace Harmonie.Application.Features.Channels.SendMessage;

public sealed class SendMessageValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageValidator()
    {
        RuleFor(x => x.Content)
            .NotNull()
            .WithMessage("Message content is required")
            .MaximumLength(MessageContent.MaxLength)
            .WithMessage($"Message content cannot exceed {MessageContent.MaxLength} characters");
    }
}
