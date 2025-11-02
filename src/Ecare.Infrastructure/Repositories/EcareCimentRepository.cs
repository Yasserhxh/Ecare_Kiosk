using Dapper;
using Ecare.Domain.Entities;
using Ecare.Shared;

namespace Ecare.Infrastructure.Repositories;

public interface IEcareCimentRepository
{
    Task<EcareCiment?> GetByIdAsync(int id, IUnitOfWork uow);
    Task<IEnumerable<EcareCiment>> GetAllAsync(IUnitOfWork uow, CancellationToken ct);
}

public sealed class EcareCimentRepository : IEcareCimentRepository
{
    public Task<EcareCiment?> GetByIdAsync(int id, IUnitOfWork uow)
        => uow.Connection.QuerySingleOrDefaultAsync<EcareCiment>(
            $"SELECT Id,Name,ImageUrl,Details,Type,Description FROM {DbTableNames.EcareCiments} WHERE Id = @id",
            new { id },
            uow.Transaction);

    public Task<IEnumerable<EcareCiment>> GetAllAsync(IUnitOfWork uow, CancellationToken ct)
    {
        var cmd = new CommandDefinition(
            $"SELECT Id,Name,ImageUrl,Details,Type,Description FROM {DbTableNames.EcareCiments} ORDER BY Name",
            transaction: uow.Transaction,
            cancellationToken: ct);
        return uow.Connection.QueryAsync<EcareCiment>(cmd);
    }
}
