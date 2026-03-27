using Harmonie.Application.Features.Guilds.AcceptInvite;
using Harmonie.Application.Features.Guilds.BanMember;
using Harmonie.Application.Features.Guilds.CreateChannel;
using Harmonie.Application.Features.Guilds.CreateGuild;
using Harmonie.Application.Features.Guilds.CreateGuildInvite;
using Harmonie.Application.Features.Guilds.DeleteGuild;
using Harmonie.Application.Features.Guilds.DeleteGuildIcon;
using Harmonie.Application.Features.Guilds.GetGuildChannels;
using Harmonie.Application.Features.Guilds.GetGuildMembers;
using Harmonie.Application.Features.Guilds.GetGuildVoiceParticipants;
using Harmonie.Application.Features.Guilds.LeaveGuild;
using Harmonie.Application.Features.Guilds.ListBans;
using Harmonie.Application.Features.Guilds.ListGuildInvites;
using Harmonie.Application.Features.Guilds.ListUserGuilds;
using Harmonie.Application.Features.Guilds.PreviewInvite;
using Harmonie.Application.Features.Guilds.RemoveMember;
using Harmonie.Application.Features.Guilds.ReorderChannels;
using Harmonie.Application.Features.Guilds.RevokeInvite;
using Harmonie.Application.Features.Guilds.SearchMessages;
using Harmonie.Application.Features.Guilds.TransferOwnership;
using Harmonie.Application.Features.Guilds.UnbanMember;
using Harmonie.Application.Features.Guilds.UpdateGuild;
using Harmonie.Application.Features.Guilds.UpdateMemberRole;

namespace Harmonie.API.Endpoints;

public static class GuildEndpoints
{
    public static void MapGuildEndpoints(this IEndpointRouteBuilder app)
    {
        CreateGuildEndpoint.Map(app);
        DeleteGuildEndpoint.Map(app);
        DeleteGuildIconEndpoint.Map(app);
        ListUserGuildsEndpoint.Map(app);
        UpdateGuildEndpoint.Map(app);
        TransferOwnershipEndpoint.Map(app);
        SearchMessagesEndpoint.Map(app);

        // Channels within guilds
        CreateChannelEndpoint.Map(app);
        GetGuildChannelsEndpoint.Map(app);
        ReorderChannelsEndpoint.Map(app);

        // Members
        GetGuildMembersEndpoint.Map(app);
        RemoveMemberEndpoint.Map(app);
        LeaveGuildEndpoint.Map(app);
        UpdateMemberRoleEndpoint.Map(app);

        // Bans
        BanMemberEndpoint.Map(app);
        ListBansEndpoint.Map(app);
        UnbanMemberEndpoint.Map(app);

        // Invites
        CreateGuildInviteEndpoint.Map(app);
        ListGuildInvitesEndpoint.Map(app);
        PreviewInviteEndpoint.Map(app);
        AcceptInviteEndpoint.Map(app);
        RevokeInviteEndpoint.Map(app);

        // Voice
        GetGuildVoiceParticipantsEndpoint.Map(app);
    }
}
