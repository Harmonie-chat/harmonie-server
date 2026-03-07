using FluentValidation;
using Harmonie.Domain.ValueObjects;

namespace Harmonie.Application.Features.Conversations.SendDirectMessage;

public sealed class SendDirectMessageValidator : AbstractValidator<SendDirectMessageRequest>
{
    public SendDirectMessageValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty()
            .WithMessage("Content is required")
            .Must(content => content is null || content.Trim().Length <= ChannelMessageContent.MaxLength)
            .WithMessage($"Content cannot exceed {ChannelMessageContent.MaxLength} characters");
    }
}
