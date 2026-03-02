using FluentValidation;

namespace Harmonie.Application.Features.Guilds.UpdateMemberRole;

public sealed class UpdateMemberRoleValidator : AbstractValidator<UpdateMemberRoleRequest>
{
    public UpdateMemberRoleValidator()
    {
        RuleFor(x => x.Role)
            .IsInEnum()
            .WithMessage("Role must be 'Admin' or 'Member'");
    }
}
