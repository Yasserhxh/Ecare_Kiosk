using System.Data;
using Dapper;
using Ecare.Shared;
using MediatR;

namespace Ecare.Application.Commands.Orders
{
    public sealed class CreateOrderFromFormHandler(IUnitOfWork uow)
        : IRequestHandler<CreateOrderFromFormCommand, Result<int>>
    {
        // Orders insert
        private const string InsertOrderSql = @"
            INSERT INTO dbo.Orders
            (
                ShippingId,         -- forced
                NumeroCommande,
                DateCommande,       -- server-side
                ChauffeurNom,
                PlaqueCamion,
                Statut,
                UserId,
                NomComplet
            )
            OUTPUT INSERTED.Id
            VALUES
            (
                @ShippingId,
                @NumeroCommande,
                SYSUTCDATETIME(),
                @ChauffeurNom,
                @PlaqueCamion,
                @Statut,
                @UserId,
                @NomComplet
            );";

        // OrderItems insert (ProductId = Quality, Quantity = QuantityT, Unite = 'Tonnes')
        private const string InsertOrderItemSql = @"
            INSERT INTO dbo.Ecare_OrderItems (OrderId, ProductId, Quantity, Unite)
            VALUES (@OrderId, @ProductId, @Quantity, @Unite);";

        public async Task<Result<int>> Handle(CreateOrderFromFormCommand cmd, CancellationToken ct)
        {
            await uow.BeginAsync(ct);
            try
            {
                // 1) Insert the Order
                var pOrder = new DynamicParameters();
                pOrder.Add("ShippingId", 1, DbType.Int32);                       // force 1
                pOrder.Add("NumeroCommande", cmd.NumeroCommande, DbType.String);
                pOrder.Add("ChauffeurNom", cmd.ChauffeurNom, DbType.String);
                pOrder.Add("PlaqueCamion", cmd.Matricule, DbType.String);
                pOrder.Add("Statut", "EnTraitement", DbType.String);
                pOrder.Add("UserId", cmd.UserId, DbType.String);
                pOrder.Add("NomComplet", cmd.ClientName, DbType.String);

                var newOrderId = await uow.Connection.ExecuteScalarAsync<int>(
                    new CommandDefinition(InsertOrderSql, pOrder, uow.Transaction, cancellationToken: ct));

                // 2) Optionally insert the Order Item if product & quantity provided
                if (cmd.ProductId.HasValue && cmd.QuantityT.HasValue)
                {
                    var pItem = new DynamicParameters();
                    pItem.Add("OrderId", newOrderId, DbType.Int32);
                    pItem.Add("ProductId", cmd.ProductId.Value, DbType.Int32);   // ProductId is the Quality
                    pItem.Add("Quantity", cmd.QuantityT.Value, DbType.Decimal);  // Quantity = QuantityT
                    pItem.Add("Unite", "Tonnes", DbType.String);                 // Unit = 'Tonnes'

                    await uow.Connection.ExecuteAsync(
                        new CommandDefinition(InsertOrderItemSql, pItem, uow.Transaction, cancellationToken: ct));
                }

                await uow.CommitAsync(ct);
                return Result<int>.Ok(newOrderId);
            }
            catch
            {
                try { await uow.RollbackAsync(ct); } catch { /* ignore */ }
                throw;
            }
        }
    }
}
