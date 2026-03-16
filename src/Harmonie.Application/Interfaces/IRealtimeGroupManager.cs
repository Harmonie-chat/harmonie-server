using Harmonie.Domain.ValueObjects;

namespace Harmonie.Application.Interfaces;

public interface IRealtimeGroupManager
{
    Task SubscribeConnectionAsync(
        UserId userId,
        string connectionId,
        CancellationToken cancellationToken = default);

    Task AddUserToGuildGroupsAsync(
        UserId userId,
        GuildId guildId,
        CancellationToken cancellationToken = default);

    Task RemoveUserFromGuildGroupsAsync(
        UserId userId,
        GuildId guildId,
        CancellationToken cancellationToken = default);

    Task AddUserToChannelGroupAsync(
        UserId userId,
        GuildChannelId channelId,
        CancellationToken cancellationToken = default);

    Task AddAllGuildMembersToChannelGroupAsync(
        GuildId guildId,
        GuildChannelId channelId,
        CancellationToken cancellationToken = default);

    Task AddUserToConversationGroupAsync(
        UserId userId,
        ConversationId conversationId,
        CancellationToken cancellationToken = default);
}
