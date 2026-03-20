using FluentValidation;

namespace Harmonie.Application.Features.Conversations.AcknowledgeRead;

public sealed class AcknowledgeReadRouteValidator : AbstractValidator<AcknowledgeReadRouteRequest>
{
    public AcknowledgeReadRouteValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty()
            .WithMessage("Conversation ID is required")
            .Must(id => Guid.TryParse(id, out var parsed) && parsed != Guid.Empty)
            .WithMessage("Conversation ID must be a valid non-empty GUID");
    }
}
