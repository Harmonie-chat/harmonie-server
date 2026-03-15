using Harmonie.Domain.ValueObjects;

namespace Harmonie.Application.Interfaces;

public interface IMessageReactionRepository
{
    Task<bool> ExistsAsync(
        MessageId messageId,
        UserId userId,
        string emoji,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        MessageId messageId,
        UserId userId,
        string emoji,
        DateTime createdAtUtc,
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
