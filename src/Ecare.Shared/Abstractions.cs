using System.Data;

namespace Ecare.Shared;
public interface IUnitOfWork
{
    IDbConnection Connection { get; }
    IDbTransaction? Transaction { get; }
    Task BeginAsync(CancellationToken ct);
    Task CommitAsync(CancellationToken ct);
    Task RollbackAsync(CancellationToken ct);
}

public interface IDbConnectionFactory
{
    IDbConnection Create();
}
