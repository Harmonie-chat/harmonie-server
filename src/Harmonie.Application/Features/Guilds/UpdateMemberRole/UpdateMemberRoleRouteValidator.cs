using FluentValidation;

namespace Harmonie.Application.Features.Guilds.UpdateMemberRole;

public sealed class UpdateMemberRoleRouteValidator : AbstractValidator<UpdateMemberRoleRouteRequest>
{
    public UpdateMemberRoleRouteValidator()
    {
        RuleFor(x => x.GuildId)
            .NotEmpty()
            .WithMessage("Guild ID is required")
            .Must(guildId => Guid.TryParse(guildId, out var parsed) && parsed != Guid.Empty)
            .WithMessage("Guild ID must be a valid non-empty GUID");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required")
            .Must(userId => Guid.TryParse(userId, out var parsed) && parsed != Guid.Empty)
            .WithMessage("User ID must be a valid non-empty GUID");
    }
}
