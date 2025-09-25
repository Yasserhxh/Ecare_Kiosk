using Dapper;
using Ecare.Domain.Entities;
using Ecare.Shared;

namespace Ecare.Infrastructure.Repositories;

public interface IEcareCimentRepository
{
    Task<EcareCiment?> GetByIdAsync(int id, IUnitOfWork uow);
}

public sealed class EcareCimentRepository : IEcareCimentRepository
{
    public Task<EcareCiment?> GetByIdAsync(int id, IUnitOfWork uow)
        => uow.Connection.QuerySingleOrDefaultAsync<EcareCiment>(
            $"SELECT * FROM {DbTableNames.EcareCiments} WHERE Id = @id",
            new { id },
            uow.Transaction);
}
