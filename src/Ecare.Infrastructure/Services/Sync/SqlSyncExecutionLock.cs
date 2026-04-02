using Ecare.Domain.Inerface.Sync;
using System.Data;

namespace Ecare.Infrastructure.Services.Sync;

public sealed class SqlSyncExecutionLock : ISyncExecutionLock
{
    private readonly IDbConnection? _connection;

    public bool Acquired { get; }

    public SqlSyncExecutionLock(IDbConnection? connection, bool acquired)
    {
        _connection = connection;
        Acquired = acquired;
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else
        {
            _connection?.Dispose();
        }
    }
}