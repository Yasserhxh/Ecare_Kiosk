using Dapper;
using Ecare.Domain.Entities;
using Ecare.Domain.ValueObjects;
using Ecare.Shared;

namespace Ecare.Infrastructure.Repositories;

public interface IDriverRepository { Task<Driver?> GetBySlvAsync(SlvId slv, IUnitOfWork uow); }

public sealed class DriverRepository : IDriverRepository
{
    public Task<Driver?> GetBySlvAsync(SlvId slv, IUnitOfWork uow)
        => uow.Connection.QuerySingleOrDefaultAsync<Driver>(
            $"SELECT TOP(1) * FROM {DbTableNames.Drivers} WHERE Slv=@slv", new { slv = slv.Value }, uow.Transaction);
}
