using Harmonie.Application.Interfaces;

namespace Harmonie.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly DbSession _dbSession;

    public UnitOfWork(DbSession dbSession)
    {
        _dbSession = dbSession;
    }

    public Task BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        _dbSession.BeginTransactionAsync(cancellationToken);

    public Task CommitAsync(CancellationToken cancellationToken = default) =>
        _dbSession.CommitAsync(cancellationToken);

    public Task RollbackAsync(CancellationToken cancellationToken = default) =>
        _dbSession.RollbackAsync(cancellationToken);
}
