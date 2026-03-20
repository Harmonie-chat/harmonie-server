using FluentValidation;

namespace Harmonie.Application.Features.Channels.AcknowledgeRead;

public sealed class AcknowledgeReadValidator : AbstractValidator<AcknowledgeReadRequest>
{
    public AcknowledgeReadValidator()
    {
        RuleFor(x => x.MessageId)
            .Must(id => id is null || (Guid.TryParse(id, out var parsed) && parsed != Guid.Empty))
            .WithMessage("Message ID must be a valid non-empty GUID when provided");
    }
}
