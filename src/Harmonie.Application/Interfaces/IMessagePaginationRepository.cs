using Harmonie.Domain.Entities;
using Harmonie.Domain.ValueObjects;

namespace Harmonie.Application.Interfaces;

public interface IMessagePaginationRepository
{
    Task<MessagePage> GetChannelPageAsync(
        GuildChannelId channelId,
        MessageCursor? beforeCursor,
        int limit,
        UserId callerId,
        CancellationToken cancellationToken = default);

    Task<MessagePage> GetConversationPageAsync(
        ConversationId conversationId,
        MessageCursor? cursor,
        int limit,
        UserId callerId,
        CancellationToken cancellationToken = default);
}
