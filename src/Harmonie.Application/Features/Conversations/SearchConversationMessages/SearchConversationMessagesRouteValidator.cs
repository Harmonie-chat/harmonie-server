using FluentValidation;

namespace Harmonie.Application.Features.Conversations.SearchConversationMessages;

public sealed class SearchConversationMessagesRouteValidator : AbstractValidator<SearchConversationMessagesRouteRequest>
{
    public SearchConversationMessagesRouteValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty()
            .WithMessage("Conversation ID is required")
            .Must(conversationId => Guid.TryParse(conversationId, out var parsed) && parsed != Guid.Empty)
            .WithMessage("Conversation ID must be a valid non-empty GUID");
    }
}
