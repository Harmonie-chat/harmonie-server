using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Interfaces.Messages;

public interface IPinnedMessageRepository
{
    Task<bool> IsPinnedAsync(
        MessageId messageId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        PinnedMessage pinnedMessage,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(
        MessageId messageId,
        CancellationToken cancellationToken = default);
}
