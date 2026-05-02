using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.ValueObjects.Messages;

namespace Harmonie.Application.Interfaces.Messages;

public interface ILinkPreviewRepository
{
    Task<IReadOnlyList<MessageLinkPreview>> GetByMessageIdsAsync(
        IReadOnlyCollection<MessageId> messageIds,
        CancellationToken cancellationToken = default);

    Task<MessageLinkPreview?> TryGetRecentPreviewAsync(
        string url,
        TimeSpan maxAge,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        MessageLinkPreview preview,
        CancellationToken cancellationToken = default);
}
