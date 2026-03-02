using Harmonie.Domain.Enums;

namespace Harmonie.Application.Features.Guilds.UpdateMemberRole;

public enum GuildRoleInput
{
    Admin = 1,
    Member = 2
}

public static class GuildRoleInputExtensions
{
    public static GuildRole ToDomain(this GuildRoleInput input) => input switch
    {
        GuildRoleInput.Admin  => GuildRole.Admin,
        GuildRoleInput.Member => GuildRole.Member,
        _                     => throw new InvalidOperationException($"Unhandled GuildRoleInput: {input}")
    };
}
