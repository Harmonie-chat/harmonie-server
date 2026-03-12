using FluentValidation;

namespace Harmonie.Application.Features.Guilds.CreateGuildInvite;

public sealed class CreateGuildInviteValidator : AbstractValidator<CreateGuildInviteRequest>
{
    public CreateGuildInviteValidator()
    {
        RuleFor(x => x.MaxUses)
            .GreaterThan(0)
            .When(x => x.MaxUses.HasValue)
            .WithMessage("Max uses must be greater than 0");

        RuleFor(x => x.ExpiresInHours)
            .GreaterThan(0)
            .When(x => x.ExpiresInHours.HasValue)
            .WithMessage("Expiration hours must be greater than 0");
    }
}
