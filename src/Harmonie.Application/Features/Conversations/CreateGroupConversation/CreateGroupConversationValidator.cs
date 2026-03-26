using FluentValidation;

namespace Harmonie.Application.Features.Conversations.CreateGroupConversation;

public sealed class CreateGroupConversationValidator : AbstractValidator<CreateGroupConversationRequest>
{
    public CreateGroupConversationValidator()
    {
        RuleFor(x => x.Name)
            .MaximumLength(100)
            .When(x => x.Name is not null)
            .WithMessage("Conversation name must be 100 characters or less");

        RuleFor(x => x.ParticipantUserIds)
            .NotEmpty()
            .WithMessage("Participant list is required")
            .Must(ids => ids.Count >= 2)
            .WithMessage("A group conversation requires at least 2 participants")
            .Must(ids => ids.All(id => id != Guid.Empty))
            .WithMessage("All participant IDs must be valid non-empty GUIDs")
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Participant list must not contain duplicate IDs");
    }
}
