using FluentValidation;

namespace Harmonie.Application.Features.Channels.RemoveReaction;

public sealed class RemoveReactionRouteValidator : AbstractValidator<RemoveReactionRouteRequest>
{
    public RemoveReactionRouteValidator()
    {
        RuleFor(x => x.Emoji)
            .NotEmpty()
            .WithMessage("Emoji is required")
            .MaximumLength(64)
            .WithMessage("Emoji must not exceed 64 characters");
    }
}
