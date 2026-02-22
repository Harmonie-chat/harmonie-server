using Harmonie.Application.Interfaces;

namespace Harmonie.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly DbSession _dbSession;

    public UnitOfWork(DbSession dbSession)
    {
        _dbSession = dbSession;
    }

    public async Task<IUnitOfWorkTransaction> BeginAsync(CancellationToken cancellationToken = default)
    {
        await _dbSession.BeginTransactionAsync(cancellationToken);
        return new UnitOfWorkTransaction(_dbSession);
    }

    private sealed class UnitOfWorkTransaction : IUnitOfWorkTransaction
    {
        private readonly DbSession _dbSession;
        private bool _committed;

        public UnitOfWorkTransaction(DbSession dbSession)
        {
            _dbSession = dbSession;
        }

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            if (_committed)
                throw new InvalidOperationException("Transaction has already been committed.");

            await _dbSession.CommitAsync(cancellationToken);
            _committed = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (!_committed)
            {
                try
                {
                    await _dbSession.RollbackAsync();
                }
                catch
                {
                    // Keep dispose non-throwing to preserve original application exceptions.
                }
            }

            try
            {
                await _dbSession.CloseConnectionAsync();
            }
            catch
            {
                // Keep dispose non-throwing to preserve original application exceptions.
            }
        }
    }
}
