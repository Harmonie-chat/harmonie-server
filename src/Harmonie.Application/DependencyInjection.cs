using System.Reflection;
using FluentValidation;
using Harmonie.Application.Features.Auth.Login;
using Harmonie.Application.Features.Auth.LogoutAll;
using Harmonie.Application.Features.Auth.Logout;
using Harmonie.Application.Features.Auth.RefreshToken;
using Harmonie.Application.Features.Auth.Register;
using Harmonie.Application.Features.Channels.DeleteChannel;
using Harmonie.Application.Features.Channels.DeleteMessage;
using Harmonie.Application.Features.Channels.EditMessage;
using Harmonie.Application.Features.Channels.GetMessages;
using Harmonie.Application.Features.Channels.JoinVoiceChannel;
using Harmonie.Application.Features.Channels.SendMessage;
using Harmonie.Application.Features.Channels.UpdateChannel;
using Harmonie.Application.Features.Conversations.DeleteDirectMessage;
using Harmonie.Application.Features.Conversations.EditDirectMessage;
using Harmonie.Application.Features.Conversations.GetDirectMessages;
using Harmonie.Application.Features.Conversations.ListConversations;
using Harmonie.Application.Features.Conversations.OpenConversation;
using Harmonie.Application.Features.Conversations.SearchConversationMessages;
using Harmonie.Application.Features.Conversations.SendDirectMessage;
using Harmonie.Application.Features.Guilds.CreateChannel;
using Harmonie.Application.Features.Guilds.CreateGuild;
using Harmonie.Application.Features.Guilds.GetGuildChannels;
using Harmonie.Application.Features.Guilds.GetGuildVoiceParticipants;
using Harmonie.Application.Features.Guilds.GetGuildMembers;
using Harmonie.Application.Features.Guilds.InviteMember;
using Harmonie.Application.Features.Guilds.LeaveGuild;
using Harmonie.Application.Features.Guilds.ListUserGuilds;
using Harmonie.Application.Features.Guilds.RemoveMember;
using Harmonie.Application.Features.Guilds.SearchMessages;
using Harmonie.Application.Features.Guilds.TransferOwnership;
using Harmonie.Application.Features.Guilds.UpdateMemberRole;
using Harmonie.Application.Features.Users.GetMyProfile;
using Harmonie.Application.Features.Users.SearchUsers;
using Harmonie.Application.Features.Users.UpdateMyProfile;
using Harmonie.Application.Features.Uploads.UploadFile;
using Harmonie.Application.Features.Voice.HandleLiveKitWebhook;
using Microsoft.Extensions.DependencyInjection;

namespace Harmonie.Application;

/// <summary>
/// Extension methods for configuring Application layer services
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Register FluentValidation validators
        services.AddValidatorsFromAssembly(assembly);

        // Register feature handlers
        // Auth features
        services.AddScoped<RegisterHandler>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<LogoutHandler>();
        services.AddScoped<LogoutAllHandler>();
        services.AddScoped<RefreshTokenHandler>();
        services.AddScoped<CreateChannelHandler>();
        services.AddScoped<CreateGuildHandler>();
        services.AddScoped<ListUserGuildsHandler>();
        services.AddScoped<InviteMemberHandler>();
        services.AddScoped<LeaveGuildHandler>();
        services.AddScoped<RemoveMemberHandler>();
        services.AddScoped<UpdateMemberRoleHandler>();
        services.AddScoped<TransferOwnershipHandler>();
        services.AddScoped<GetGuildChannelsHandler>();
        services.AddScoped<GetGuildVoiceParticipantsHandler>();
        services.AddScoped<GetGuildMembersHandler>();
        services.AddScoped<SearchMessagesHandler>();
        services.AddScoped<SendMessageHandler>();
        services.AddScoped<GetMessagesHandler>();
        services.AddScoped<JoinVoiceChannelHandler>();
        services.AddScoped<GetMyProfileHandler>();
        services.AddScoped<SearchUsersHandler>();
        services.AddScoped<UpdateMyProfileHandler>();
        services.AddScoped<UploadFileHandler>();
        services.AddScoped<HandleLiveKitWebhookHandler>();
        services.AddScoped<UpdateChannelHandler>();
        services.AddScoped<DeleteChannelHandler>();
        services.AddScoped<EditMessageHandler>();
        services.AddScoped<DeleteMessageHandler>();
        services.AddScoped<OpenConversationHandler>();
        services.AddScoped<ListConversationsHandler>();
        services.AddScoped<GetDirectMessagesHandler>();
        services.AddScoped<SearchConversationMessagesHandler>();
        services.AddScoped<EditDirectMessageHandler>();
        services.AddScoped<DeleteDirectMessageHandler>();
        services.AddScoped<SendDirectMessageHandler>();
        // Add more handlers as features are created

        return services;
    }
}
