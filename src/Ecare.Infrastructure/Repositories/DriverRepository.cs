using Dapper;
using Ecare.Domain.Entities;
using Ecare.Domain.ValueObjects;
using Ecare.Shared;

namespace Ecare.Infrastructure.Repositories;

public interface IDriverRepository
{
    Task<ClientEquipement?> GetBySlvAsync(SlvId slv, IUnitOfWork uow);
}

public sealed class DriverRepository : IDriverRepository
{
    public Task<ClientEquipement?> GetBySlvAsync(SlvId slv, IUnitOfWork uow)
        => uow.Connection.QuerySingleOrDefaultAsync<ClientEquipement>(
            $"SELECT TOP(1) * FROM {DbTableNames.ClientEquipements} WHERE CarteSLV = @slv",
            new { slv = slv.Value },
            uow.Transaction);
}
