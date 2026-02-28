using FluentValidation;

namespace Harmonie.Application.Features.Guilds.LeaveGuild;

public sealed class LeaveGuildRouteValidator : AbstractValidator<LeaveGuildRouteRequest>
{
    public LeaveGuildRouteValidator()
    {
        RuleFor(x => x.GuildId)
            .NotEmpty()
            .WithMessage("Guild ID is required")
            .Must(guildId => Guid.TryParse(guildId, out var parsed) && parsed != Guid.Empty)
            .WithMessage("Guild ID must be a valid non-empty GUID");
    }
}
