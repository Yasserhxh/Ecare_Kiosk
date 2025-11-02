using Dapper;
using Ecare.Domain.Entities;
using Ecare.Shared;

namespace Ecare.Infrastructure.Repositories;

public interface IClientEquipementRepository
{
    Task<ClientEquipement?> GetByCarteSlvAsync(string carteSlv, IUnitOfWork uow);
}

public sealed class ClientEquipementRepository : IClientEquipementRepository
{
    public Task<ClientEquipement?> GetByCarteSlvAsync(string carteSlv, IUnitOfWork uow)
        => uow.Connection.QuerySingleOrDefaultAsync<ClientEquipement>(
            $"SELECT TOP(1) * FROM {DbTableNames.ClientEquipements} WHERE CarteSLV = @carteSlv",
            new { carteSlv },
            uow.Transaction);
}
