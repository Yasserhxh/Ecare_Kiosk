using Dapper;
using Ecare.Domain.Entities;
using Ecare.Domain;
using Ecare.Shared;

namespace Ecare.Infrastructure.Repositories;

public interface IOrderRepository
{
    Task<Order?> GetByNumberAsync(string number, IUnitOfWork uow);
    Task<Order?> GetByDriverAsync(Guid driverId, IUnitOfWork uow);
    Task<Guid> InsertAsync(Order o, IUnitOfWork uow);
    Task UpdateStatusAsync(Guid id, OrderStatus status, IUnitOfWork uow);
}

public sealed class OrderRepository : IOrderRepository
{
    public Task<Order?> GetByNumberAsync(string number, IUnitOfWork uow)
        => uow.Connection.QuerySingleOrDefaultAsync<Order>(
            $"SELECT * FROM {DbTableNames.Orders} WHERE Number=@number", new { number }, uow.Transaction);

    public Task<Order?> GetByDriverAsync(Guid driverId, IUnitOfWork uow)
        => uow.Connection.QuerySingleOrDefaultAsync<Order>(
            $"SELECT TOP(1) * FROM {DbTableNames.Orders} WHERE DriverId=@driverId AND Status<>@s ORDER BY CreatedAt DESC",
            new { driverId, s = OrderStatus.Annulee }, uow.Transaction);

    public async Task<Guid> InsertAsync(Order o, IUnitOfWork uow)
    {
        o.Id = Guid.NewGuid();
        await uow.Connection.ExecuteAsync($@"INSERT INTO {DbTableNames.Orders}
            (Id,Number,DriverId,ClientId,ProductId,ProductType,ProductName,Unit,Quantity,Status,CreatedAt)
            VALUES (@Id,@Number,@DriverId,@ClientId,@ProductId,@ProductType,@ProductName,@Unit,@Quantity,@Status,SYSUTCDATETIME())",
            o, uow.Transaction);
        return o.Id;
    }

    public Task UpdateStatusAsync(Guid id, OrderStatus status, IUnitOfWork uow)
        => uow.Connection.ExecuteAsync($"UPDATE {DbTableNames.Orders} SET Status=@status WHERE Id=@id", new { id, status }, uow.Transaction);
}
