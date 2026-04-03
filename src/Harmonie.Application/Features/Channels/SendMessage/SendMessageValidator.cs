using FluentValidation;

namespace Harmonie.Application.Features.Channels.SendMessage;

public sealed class SendMessageValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageValidator()
    {
        RuleForEach(x => x.AttachmentFileIds)
            .NotEqual(Guid.Empty)
            .WithMessage("Attachment file IDs must be valid non-empty GUIDs")
            .When(x => x.AttachmentFileIds is not null);

        RuleFor(x => x.AttachmentFileIds)
            .Must(ids => ids is null || ids.Distinct().Count() == ids.Count)
            .WithMessage("Attachment file IDs must be unique")
            .When(x => x.AttachmentFileIds is not null);
    }
}
