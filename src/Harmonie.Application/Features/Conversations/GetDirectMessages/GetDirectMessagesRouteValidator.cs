using FluentValidation;

namespace Harmonie.Application.Features.Conversations.GetDirectMessages;

public sealed class GetDirectMessagesRouteValidator : AbstractValidator<GetDirectMessagesRouteRequest>
{
    public GetDirectMessagesRouteValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty()
            .WithMessage("Conversation ID is required")
            .Must(conversationId => Guid.TryParse(conversationId, out var parsed) && parsed != Guid.Empty)
            .WithMessage("Conversation ID must be a valid non-empty GUID");
    }
}
