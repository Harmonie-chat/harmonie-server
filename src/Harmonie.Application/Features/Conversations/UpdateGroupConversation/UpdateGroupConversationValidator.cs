using FluentValidation;

namespace Harmonie.Application.Features.Conversations.UpdateGroupConversation;

public sealed class UpdateGroupConversationValidator : AbstractValidator<UpdateGroupConversationRequest>
{
    public UpdateGroupConversationValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Conversation name cannot be empty")
            .When(x => x.Name is not null);

        RuleFor(x => x.Name)
            .MaximumLength(100)
            .WithMessage("Conversation name must be 100 characters or less")
            .When(x => x.Name is not null);
    }
}
