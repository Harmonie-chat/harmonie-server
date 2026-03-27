using Harmonie.Application.Common;
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
using Harmonie.Domain.ValueObjects.Guilds;
using Microsoft.Extensions.DependencyInjection;

namespace Harmonie.Application.Registration;

public static class GuildRegistration
{
    public static IServiceCollection AddGuildHandlers(this IServiceCollection services)
    {
        services.AddAuthenticatedHandler<CreateGuildRequest, CreateGuildResponse, CreateGuildHandler>();
        services.AddAuthenticatedHandler<DeleteGuildInput, bool, DeleteGuildHandler>();
        services.AddAuthenticatedHandler<DeleteGuildIconInput, bool, DeleteGuildIconHandler>();
        services.AddAuthenticatedHandler<Unit, ListUserGuildsResponse, ListUserGuildsHandler>();
        services.AddAuthenticatedHandler<UpdateGuildInput, UpdateGuildResponse, UpdateGuildHandler>();
        services.AddAuthenticatedHandler<TransferOwnershipInput, bool, TransferOwnershipHandler>();
        services.AddAuthenticatedHandler<SearchMessagesInput, SearchMessagesResponse, SearchMessagesHandler>();

        // Channels within guilds
        services.AddAuthenticatedHandler<CreateChannelInput, CreateChannelResponse, CreateChannelHandler>();
        services.AddAuthenticatedHandler<GuildId, GetGuildChannelsResponse, GetGuildChannelsHandler>();
        services.AddAuthenticatedHandler<ReorderChannelsInput, ReorderChannelsResponse, ReorderChannelsHandler>();

        // Members
        services.AddAuthenticatedHandler<GuildId, GetGuildMembersResponse, GetGuildMembersHandler>();
        services.AddAuthenticatedHandler<RemoveMemberInput, bool, RemoveMemberHandler>();
        services.AddAuthenticatedHandler<LeaveGuildInput, bool, LeaveGuildHandler>();
        services.AddAuthenticatedHandler<UpdateMemberRoleInput, bool, UpdateMemberRoleHandler>();

        // Bans
        services.AddAuthenticatedHandler<BanMemberInput, BanMemberResponse, BanMemberHandler>();
        services.AddAuthenticatedHandler<GuildId, ListBansResponse, ListBansHandler>();
        services.AddAuthenticatedHandler<UnbanMemberInput, bool, UnbanMemberHandler>();

        // Invites
        services.AddAuthenticatedHandler<CreateGuildInviteInput, CreateGuildInviteResponse, CreateGuildInviteHandler>();
        services.AddAuthenticatedHandler<GuildId, ListGuildInvitesResponse, ListGuildInvitesHandler>();
        services.AddHandler<string, PreviewInviteResponse, PreviewInviteHandler>();
        services.AddAuthenticatedHandler<string, AcceptInviteResponse, AcceptInviteHandler>();
        services.AddAuthenticatedHandler<RevokeInviteInput, bool, RevokeInviteHandler>();

        // Voice
        services.AddAuthenticatedHandler<GuildId, GetGuildVoiceParticipantsResponse, GetGuildVoiceParticipantsHandler>();

        return services;
    }
}
