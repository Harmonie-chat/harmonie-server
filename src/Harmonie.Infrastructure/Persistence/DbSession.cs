using Npgsql;

namespace Harmonie.Infrastructure.Persistence;

public sealed class DbSession : IAsyncDisposable
{
    private readonly string _connectionString;
    private NpgsqlConnection? _connection;
    private NpgsqlTransaction? _transaction;

    public DbSession(string connectionString)
    {
        _connectionString = connectionString;
    }

    public NpgsqlTransaction? Transaction => _transaction;

    public async Task<NpgsqlConnection> GetOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is null)
            _connection = new NpgsqlConnection(_connectionString);

        if (_connection.State != System.Data.ConnectionState.Open)
            await _connection.OpenAsync(cancellationToken);

        return _connection;
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
            throw new InvalidOperationException("A transaction is already active.");

        var connection = await GetOpenConnectionAsync(cancellationToken);
        _transaction = await connection.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
            throw new InvalidOperationException("No active transaction to commit.");

        await _transaction.CommitAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
            return;

        await _transaction.RollbackAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            try
            {
                await _transaction.RollbackAsync();
            }
            catch
            {
                // Best effort rollback on scope dispose.
            }

            await _transaction.DisposeAsync();
            _transaction = null;
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}
