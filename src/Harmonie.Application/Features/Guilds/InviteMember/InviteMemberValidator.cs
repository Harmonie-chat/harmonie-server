using FluentValidation;

namespace Harmonie.Application.Features.Guilds.InviteMember;

public sealed class InviteMemberValidator : AbstractValidator<InviteMemberRequest>
{
    public InviteMemberValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required")
            .Must(userId => Guid.TryParse(userId, out var parsed) && parsed != Guid.Empty)
            .WithMessage("User ID must be a valid non-empty GUID");
    }
}
