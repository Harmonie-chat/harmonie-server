using FluentValidation;
using Harmonie.Domain.Enums;

namespace Harmonie.Application.Features.Guilds.UpdateMemberRole;

public sealed class UpdateMemberRoleValidator : AbstractValidator<UpdateMemberRoleRequest>
{
    public UpdateMemberRoleValidator()
    {
        RuleFor(x => x.Role)
            .NotEmpty()
            .WithMessage("Role is required")
            .Must(role => Enum.TryParse<GuildRole>(role, ignoreCase: true, out _))
            .WithMessage("Role must be 'Admin' or 'Member'");
    }
}
