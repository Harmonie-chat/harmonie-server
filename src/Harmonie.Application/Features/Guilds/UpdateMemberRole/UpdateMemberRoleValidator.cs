using FluentValidation;

namespace Harmonie.Application.Features.Guilds.UpdateMemberRole;

public sealed class UpdateMemberRoleValidator : AbstractValidator<UpdateMemberRoleRequest>
{
    // No rules needed: GuildRoleInput deserialization rejects unknown/numeric values
}
