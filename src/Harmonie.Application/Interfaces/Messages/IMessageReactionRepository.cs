using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Interfaces.Messages;

public interface IMessageReactionRepository
{
    Task<bool> ExistsAsync(
        MessageId messageId,
        UserId userId,
        string emoji,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        MessageReaction reaction,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(
        MessageId messageId,
        UserId userId,
        string emoji,
        CancellationToken cancellationToken = default);
}

public sealed record MessageReactionSummary(
    string Emoji,
    int Count,
    bool ReactedByCaller);
