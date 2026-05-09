using FluentValidation;

namespace Harmonie.Application.Features.Channels.SendMessage;

public sealed class SendMessageValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageValidator()
    {
        RuleFor(x => x.ReplyToMessageId)
            .NotEqual(Guid.Empty)
            .WithMessage("ReplyToMessageId must be a valid non-empty GUID")
            .When(x => x.ReplyToMessageId.HasValue);

        RuleForEach(x => x.AttachmentFileIds)
            .NotEqual(Guid.Empty)
            .WithMessage("Attachment file IDs must be valid non-empty GUIDs")
            .When(x => x.AttachmentFileIds is not null);

        RuleFor(x => x.AttachmentFileIds)
            .Must(ids => ids is null || ids.Distinct().Count() == ids.Count)
            .WithMessage("Attachment file IDs must be unique")
            .When(x => x.AttachmentFileIds is not null);

        RuleForEach(x => x.MentionedUserIds)
            .NotEqual(Guid.Empty)
            .WithMessage("Mentioned user IDs must be valid non-empty GUIDs")
            .When(x => x.MentionedUserIds is not null);

        RuleFor(x => x.MentionedUserIds)
            .Must(ids => ids is null || ids.Distinct().Count() == ids.Count)
            .WithMessage("Mentioned user IDs must be unique")
            .When(x => x.MentionedUserIds is not null);

        RuleFor(x => x.MentionedUserIds)
            .Must(ids => ids is null || ids.Count <= 50)
            .WithMessage("A message can mention at most 50 users")
            .When(x => x.MentionedUserIds is not null);
    }
}
