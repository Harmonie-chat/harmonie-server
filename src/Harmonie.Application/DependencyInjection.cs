using System.Reflection;
using FluentValidation;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Auth.Login;
using Harmonie.Application.Features.Auth.LogoutAll;
using Harmonie.Application.Features.Auth.Logout;
using Harmonie.Application.Features.Auth.RefreshToken;
using Harmonie.Application.Features.Auth.Register;
using Harmonie.Application.Features.Channels.DeleteChannel;
using Harmonie.Application.Features.Channels.DeleteMessageAttachment;
using ChannelDeleteMessageHandler = Harmonie.Application.Features.Channels.DeleteMessage.DeleteMessageHandler;
using ChannelEditMessageHandler = Harmonie.Application.Features.Channels.EditMessage.EditMessageHandler;
using ChannelGetMessagesHandler = Harmonie.Application.Features.Channels.GetMessages.GetMessagesHandler;
using Harmonie.Application.Features.Channels.JoinVoiceChannel;
using ChannelSendMessageHandler = Harmonie.Application.Features.Channels.SendMessage.SendMessageHandler;
using Harmonie.Application.Features.Channels.UpdateChannel;
using ConversationDeleteMessageAttachmentHandler = Harmonie.Application.Features.Conversations.DeleteMessageAttachment.DeleteMessageAttachmentHandler;
using ConversationDeleteMessageHandler = Harmonie.Application.Features.Conversations.DeleteMessage.DeleteMessageHandler;
using ConversationEditMessageHandler = Harmonie.Application.Features.Conversations.EditMessage.EditMessageHandler;
using ConversationGetMessagesHandler = Harmonie.Application.Features.Conversations.GetMessages.GetMessagesHandler;
using Harmonie.Application.Features.Conversations.ListConversations;
using Harmonie.Application.Features.Conversations.OpenConversation;
using Harmonie.Application.Features.Conversations.SearchConversationMessages;
using ConversationSendMessageHandler = Harmonie.Application.Features.Conversations.SendMessage.SendMessageHandler;
using Harmonie.Application.Features.Guilds.CreateChannel;
using Harmonie.Application.Features.Guilds.CreateGuild;
using Harmonie.Application.Features.Guilds.AcceptInvite;
using Harmonie.Application.Features.Guilds.CreateGuildInvite;
using Harmonie.Application.Features.Guilds.DeleteGuild;
using Harmonie.Application.Features.Guilds.DeleteGuildIcon;
using Harmonie.Application.Features.Guilds.ListGuildInvites;
using Harmonie.Application.Features.Guilds.PreviewInvite;
using Harmonie.Application.Features.Guilds.RevokeInvite;
using Harmonie.Application.Features.Guilds.GetGuildChannels;
using Harmonie.Application.Features.Guilds.ReorderChannels;
using Harmonie.Application.Features.Guilds.GetGuildVoiceParticipants;
using Harmonie.Application.Features.Guilds.GetGuildMembers;
using Harmonie.Application.Features.Guilds.InviteMember;
using Harmonie.Application.Features.Guilds.LeaveGuild;
using Harmonie.Application.Features.Guilds.ListUserGuilds;
using Harmonie.Application.Features.Guilds.RemoveMember;
using Harmonie.Application.Features.Guilds.SearchMessages;
using Harmonie.Application.Features.Guilds.TransferOwnership;
using Harmonie.Application.Features.Guilds.UpdateGuild;
using Harmonie.Application.Features.Guilds.UpdateMemberRole;
using Harmonie.Application.Features.Users.DeleteMyAvatar;
using Harmonie.Application.Features.Users.GetMyProfile;
using Harmonie.Application.Features.Users.SearchUsers;
using Harmonie.Application.Features.Users.UpdateMyProfile;
using Harmonie.Application.Features.Users.UpdateUserStatus;
using Harmonie.Application.Features.Users.UploadMyAvatar;
using Harmonie.Application.Features.Uploads.DownloadFile;
using Harmonie.Application.Features.Uploads.UploadFile;
using ChannelAddReactionHandler = Harmonie.Application.Features.Channels.AddReaction.AddReactionHandler;
using ChannelRemoveReactionHandler = Harmonie.Application.Features.Channels.RemoveReaction.RemoveReactionHandler;
using ConversationAddReactionHandler = Harmonie.Application.Features.Conversations.AddReaction.AddReactionHandler;
using ConversationRemoveReactionHandler = Harmonie.Application.Features.Conversations.RemoveReaction.RemoveReactionHandler;
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
        services.AddScoped<UploadedFileCleanupService>();
        services.AddScoped<MessageAttachmentResolver>();

        // Register feature handlers
        // Auth features
        services.AddScoped<RegisterHandler>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<LogoutHandler>();
        services.AddScoped<LogoutAllHandler>();
        services.AddScoped<RefreshTokenHandler>();
        services.AddScoped<CreateChannelHandler>();
        services.AddScoped<CreateGuildHandler>();
        services.AddScoped<CreateGuildInviteHandler>();
        services.AddScoped<ListGuildInvitesHandler>();
        services.AddScoped<PreviewInviteHandler>();
        services.AddScoped<AcceptInviteHandler>();
        services.AddScoped<RevokeInviteHandler>();
        services.AddScoped<DeleteGuildHandler>();
        services.AddScoped<DeleteGuildIconHandler>();
        services.AddScoped<ListUserGuildsHandler>();
        services.AddScoped<InviteMemberHandler>();
        services.AddScoped<LeaveGuildHandler>();
        services.AddScoped<RemoveMemberHandler>();
        services.AddScoped<UpdateMemberRoleHandler>();
        services.AddScoped<TransferOwnershipHandler>();
        services.AddScoped<UpdateGuildHandler>();
        services.AddScoped<GetGuildChannelsHandler>();
        services.AddScoped<ReorderChannelsHandler>();
        services.AddScoped<GetGuildVoiceParticipantsHandler>();
        services.AddScoped<GetGuildMembersHandler>();
        services.AddScoped<SearchMessagesHandler>();
        services.AddScoped<ChannelSendMessageHandler>();
        services.AddScoped<ChannelGetMessagesHandler>();
        services.AddScoped<JoinVoiceChannelHandler>();
        services.AddScoped<DeleteMyAvatarHandler>();
        services.AddScoped<GetMyProfileHandler>();
        services.AddScoped<SearchUsersHandler>();
        services.AddScoped<UpdateMyProfileHandler>();
        services.AddScoped<UpdateUserStatusHandler>();
        services.AddScoped<UploadMyAvatarHandler>();
        services.AddScoped<UploadFileHandler>();
        services.AddScoped<DownloadFileHandler>();
        services.AddScoped<HandleLiveKitWebhookHandler>();
        services.AddScoped<UpdateChannelHandler>();
        services.AddScoped<DeleteChannelHandler>();
        services.AddScoped<DeleteMessageAttachmentHandler>();
        services.AddScoped<ChannelEditMessageHandler>();
        services.AddScoped<ChannelDeleteMessageHandler>();
        services.AddScoped<OpenConversationHandler>();
        services.AddScoped<ListConversationsHandler>();
        services.AddScoped<ConversationGetMessagesHandler>();
        services.AddScoped<SearchConversationMessagesHandler>();
        services.AddScoped<ConversationEditMessageHandler>();
        services.AddScoped<ConversationDeleteMessageHandler>();
        services.AddScoped<ConversationDeleteMessageAttachmentHandler>();
        services.AddScoped<ConversationSendMessageHandler>();
        services.AddScoped<ChannelAddReactionHandler>();
        services.AddScoped<ChannelRemoveReactionHandler>();
        services.AddScoped<ConversationAddReactionHandler>();
        services.AddScoped<ConversationRemoveReactionHandler>();
        // Add more handlers as features are created

        return services;
    }
}
