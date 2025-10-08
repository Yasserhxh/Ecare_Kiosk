using Dapper;
using Ecare.Shared;

namespace Ecare.Infrastructure.Repositories;

public interface IKioskOrderRepository
{
    Task<Guid?> CreateAsync(Guid driverId, Guid clientId, int productId, int productType, string productName, string unit, decimal quantity, IUnitOfWork uow, CancellationToken ct);
    Task<(Guid Id, string Number, int ProductId, int ProductType, string ProductName, string Unit, decimal Quantity, int Status)?> GetActiveByDriverAsync(Guid driverId, IUnitOfWork uow, CancellationToken ct);
}

public sealed class KioskOrderRepository : IKioskOrderRepository
{
    public async Task<Guid?> CreateAsync(Guid driverId, Guid clientId, int productId, int productType, string productName, string unit, decimal quantity, IUnitOfWork uow, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        var number = $"CMD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpper()}";
        var cmd = new CommandDefinition(
            $@"INSERT INTO {DbTableNames.KioskOrders}(Id,Number,DriverId,ClientId,ProductId,ProductType,ProductName,Unit,Quantity,Status,CreatedAt)
                VALUES (@Id,@Number,@DriverId,@ClientId,@ProductId,@ProductType,@ProductName,@Unit,@Quantity,@Status,SYSUTCDATETIME())",
            new { Id = id, Number = number, DriverId = driverId, ClientId = clientId, ProductId = productId, ProductType = productType, ProductName = productName, Unit = unit, Quantity = quantity, Status = 1 },
            transaction: uow.Transaction, cancellationToken: ct);
        var rows = await uow.Connection.ExecuteAsync(cmd);
        return rows == 1 ? id : null;
    }

    public async Task<(Guid Id, string Number, int ProductId, int ProductType, string ProductName, string Unit, decimal Quantity, int Status)?> GetActiveByDriverAsync(Guid driverId, IUnitOfWork uow, CancellationToken ct)
    {
        var cmd = new CommandDefinition(
            $"SELECT TOP(1) Id,Number,ProductId,ProductType,ProductName,Unit,Quantity,Status FROM {DbTableNames.KioskOrders} WHERE DriverId=@driverId AND Status IN (1,2) ORDER BY CreatedAt DESC",
            new { driverId }, transaction: uow.Transaction, cancellationToken: ct);
        var row = await uow.Connection.QuerySingleOrDefaultAsync<(Guid, string, int, int, string, string, decimal, int)>(cmd);
        return row;
    }
}


