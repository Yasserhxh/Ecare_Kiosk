using Dapper;
using Ecare.Domain.Entities;
using Ecare.Shared;

namespace Ecare.Infrastructure.Repositories;

public interface IOrderItemRepository
{
    Task<IEnumerable<OrderItem>> GetByOrderIdAsync(int orderId, IUnitOfWork uow);
}

public sealed class OrderItemRepository : IOrderItemRepository
{
    public Task<IEnumerable<OrderItem>> GetByOrderIdAsync(int orderId, IUnitOfWork uow)
        => uow.Connection.QueryAsync<OrderItem>(
            $"SELECT * FROM {DbTableNames.EcareOrderItems} WHERE OrderId = @orderId",
            new { orderId },
            uow.Transaction);
}
