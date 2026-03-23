using FluentValidation;

namespace Harmonie.Application.Features.Channels.SendMessage;

public sealed class SendMessageValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageValidator()
    {
        RuleForEach(x => x.AttachmentFileIds)
            .Must(id => Guid.TryParse(id, out var parsedId) && parsedId != Guid.Empty)
            .WithMessage("Attachment file IDs must be valid non-empty GUIDs")
            .When(x => x.AttachmentFileIds is not null);

        RuleFor(x => x.AttachmentFileIds)
            .Must(ids => ids is null || ids.Distinct(StringComparer.OrdinalIgnoreCase).Count() == ids.Count)
            .WithMessage("Attachment file IDs must be unique")
            .When(x => x.AttachmentFileIds is not null);
    }
}
