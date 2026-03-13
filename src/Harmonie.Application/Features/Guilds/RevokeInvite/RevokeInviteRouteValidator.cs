using FluentValidation;

namespace Harmonie.Application.Features.Guilds.RevokeInvite;

public sealed class RevokeInviteRouteValidator : AbstractValidator<RevokeInviteRouteRequest>
{
    public RevokeInviteRouteValidator()
    {
        RuleFor(x => x.GuildId)
            .NotEmpty()
            .WithMessage("Guild ID is required")
            .Must(id => Guid.TryParse(id, out _))
            .WithMessage("Guild ID must be a valid GUID");

        RuleFor(x => x.InviteCode)
            .NotEmpty()
            .WithMessage("Invite code is required")
            .Length(8)
            .WithMessage("Invite code must be exactly 8 characters")
            .Matches("^[A-Za-z0-9]+$")
            .WithMessage("Invite code must be alphanumeric");
    }
}
