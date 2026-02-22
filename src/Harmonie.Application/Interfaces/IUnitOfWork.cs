namespace Harmonie.Application.Interfaces;

/// <summary>
/// Coordinates transactional boundaries for application use cases.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Begin a new database transaction.
    /// </summary>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commit current transaction.
    /// </summary>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rollback current transaction.
    /// </summary>
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
