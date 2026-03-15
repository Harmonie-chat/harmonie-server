using FluentValidation;

namespace Harmonie.Application.Features.Conversations.AddReaction;

public sealed class AddReactionRouteValidator : AbstractValidator<AddReactionRouteRequest>
{
    public AddReactionRouteValidator()
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

        RuleFor(x => x.Emoji)
            .NotEmpty()
            .WithMessage("Emoji is required")
            .MaximumLength(64)
            .WithMessage("Emoji must not exceed 64 characters");
    }
}
