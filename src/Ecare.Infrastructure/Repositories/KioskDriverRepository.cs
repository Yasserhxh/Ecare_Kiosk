using Dapper;
using Ecare.Shared;

namespace Ecare.Infrastructure.Repositories;

public interface IKioskDriverRepository
{
    Task<(Guid Id, string Slv, string Plate, Guid ClientId)?> GetBySlvAsync(string slv, IUnitOfWork uow, CancellationToken ct);
}

public sealed class KioskDriverRepository : IKioskDriverRepository
{
    public async Task<(Guid Id, string Slv, string Plate, Guid ClientId)?> GetBySlvAsync(string slv, IUnitOfWork uow, CancellationToken ct)
    {
        var cmd = new CommandDefinition(
            $"SELECT Id, Slv, Plate, ClientId FROM {DbTableNames.KioskDrivers} WHERE Slv=@slv",
            new { slv }, transaction: uow.Transaction, cancellationToken: ct);
        var row = await uow.Connection.QuerySingleOrDefaultAsync<(Guid Id, string Slv, string Plate, Guid ClientId)>(cmd);
        return row;
    }
}


