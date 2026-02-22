namespace Harmonie.Application.Interfaces;

/// <summary>
/// Coordinates transactional boundaries for application use cases.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Begin a new transactional scope.
    /// </summary>
    Task<IUnitOfWorkTransaction> BeginAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an active unit of work transaction scope.
/// </summary>
public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    /// <summary>
    /// Commit the current transactional scope.
    /// </summary>
    Task CommitAsync(CancellationToken cancellationToken = default);
}
