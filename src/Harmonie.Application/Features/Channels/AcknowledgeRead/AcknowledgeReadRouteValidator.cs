using FluentValidation;

namespace Harmonie.Application.Features.Channels.AcknowledgeRead;

public sealed class AcknowledgeReadRouteValidator : AbstractValidator<AcknowledgeReadRouteRequest>
{
    public AcknowledgeReadRouteValidator()
    {
        RuleFor(x => x.ChannelId)
            .NotEmpty()
            .WithMessage("Channel ID is required")
            .Must(id => Guid.TryParse(id, out var parsed) && parsed != Guid.Empty)
            .WithMessage("Channel ID must be a valid non-empty GUID");
    }
}
