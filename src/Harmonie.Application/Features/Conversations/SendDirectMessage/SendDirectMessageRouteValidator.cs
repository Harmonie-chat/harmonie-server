using FluentValidation;

namespace Harmonie.Application.Features.Conversations.SendDirectMessage;

public sealed class SendDirectMessageRouteValidator : AbstractValidator<SendDirectMessageRouteRequest>
{
    public SendDirectMessageRouteValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty()
            .WithMessage("Conversation ID is required")
            .Must(conversationId => Guid.TryParse(conversationId, out var parsed) && parsed != Guid.Empty)
            .WithMessage("Conversation ID must be a valid non-empty GUID");
    }
}
