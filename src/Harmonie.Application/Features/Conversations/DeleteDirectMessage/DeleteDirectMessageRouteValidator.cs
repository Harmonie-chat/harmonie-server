using FluentValidation;

namespace Harmonie.Application.Features.Conversations.DeleteDirectMessage;

public sealed class DeleteDirectMessageRouteValidator : AbstractValidator<DeleteDirectMessageRouteRequest>
{
    public DeleteDirectMessageRouteValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty()
            .WithMessage("Conversation ID is required")
            .Must(id => Guid.TryParse(id, out var parsed) && parsed != Guid.Empty)
            .WithMessage("Conversation ID must be a valid non-empty GUID");

        RuleFor(x => x.MessageId)
            .NotEmpty()
            .WithMessage("Message ID is required")
            .Must(id => Guid.TryParse(id, out var parsed) && parsed != Guid.Empty)
            .WithMessage("Message ID must be a valid non-empty GUID");
    }
}
