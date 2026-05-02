using Harmonie.Domain.Entities.Channels;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Interfaces.Channels;

public interface IChannelReadStateRepository
{
    Task UpsertAsync(
        ChannelReadState state,
        CancellationToken cancellationToken = default);

    Task<ChannelReadState?> GetAsync(
        UserId userId,
        GuildChannelId channelId,
        CancellationToken cancellationToken = default);
}
