using System.Data;
using Ecare.Shared;

namespace Ecare.Infrastructure;
public sealed class DapperUnitOfWork(IDbConnectionFactory factory) : IUnitOfWork
{
    public IDbConnection Connection { get; private set; } = default!;
    public IDbTransaction? Transaction { get; private set; }

    public async Task BeginAsync(CancellationToken ct)
    {
        Connection = factory.Create();
        if (Connection.State != ConnectionState.Open)
            await ((dynamic)Connection).OpenAsync(ct);
        Transaction = Connection.BeginTransaction();
    }

    public Task CommitAsync(CancellationToken ct)
    { Transaction?.Commit(); return Task.CompletedTask; }

    public Task RollbackAsync(CancellationToken ct)
    { Transaction?.Rollback(); return Task.CompletedTask; }
}
