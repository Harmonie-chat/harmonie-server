using FluentValidation;

namespace Harmonie.Application.Features.Conversations.AddReaction;

public sealed class AddReactionRouteValidator : AbstractValidator<AddReactionRouteRequest>
{
    public AddReactionRouteValidator()
    {
        RuleFor(x => x.Emoji)
            .NotEmpty()
            .WithMessage("Emoji is required")
            .MaximumLength(64)
            .WithMessage("Emoji must not exceed 64 characters");
    }
}
