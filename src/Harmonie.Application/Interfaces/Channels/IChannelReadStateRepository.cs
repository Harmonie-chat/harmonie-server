using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Interfaces.Channels;

public interface IChannelReadStateRepository
{
    Task UpsertAsync(
        MessageReadState state,
        CancellationToken cancellationToken = default);

    Task<MessageReadState?> GetAsync(
        UserId userId,
        GuildChannelId channelId,
        CancellationToken cancellationToken = default);
}
