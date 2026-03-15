using FluentValidation;

namespace Harmonie.Application.Features.Channels.RemoveReaction;

public sealed class RemoveReactionRouteValidator : AbstractValidator<RemoveReactionRouteRequest>
{
    public RemoveReactionRouteValidator()
    {
        RuleFor(x => x.ChannelId)
            .NotEmpty()
            .WithMessage("Channel ID is required")
            .Must(id => Guid.TryParse(id, out var parsed) && parsed != Guid.Empty)
            .WithMessage("Channel ID must be a valid non-empty GUID");

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
