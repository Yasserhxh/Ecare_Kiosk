using Dapper;
using Ecare.Domain.Entities;
using Ecare.Shared;
using MediatR;
using System.Data;

namespace Ecare.Application.Commands.Orders
{
    public sealed class CreateOrderFromFormHandler(IUnitOfWork uow)
        : IRequestHandler<CreateOrderFromFormCommand, Result<int>>
    {
        // Insert into Orders (includes CarteSLV)
        private const string InsertOrderSql = @"
            INSERT INTO dbo.Orders
            (
                ShippingId,
                NumeroCommande,
                DateCommande,
                ChauffeurNom,
                PlaqueCamion,
                CarteSLV,
                Statut,
                UserId,
                NomComplet
            )
            OUTPUT INSERTED.Id
            VALUES
            (
                @ShippingId,
                @NumeroCommande,
                CONVERT(datetime, SYSDATETIMEOFFSET() AT TIME ZONE 'Morocco Standard Time'),
                @ChauffeurNom,
                @PlaqueCamion,
                @CarteSLV,
                @Statut,
                @UserId,
                @NomComplet
            );";

        // OrderItems insert
        private const string InsertOrderItemSql = @"
            INSERT INTO dbo.Ecare_OrderItems (OrderId, ProductId, Quantity, Unite)
            VALUES (@OrderId, @ProductId, @Quantity, @Unite);";

        // Update queue : CarteSlv column in Ecare_Queue
        private const string UpdateQueueSql = @"
            UPDATE dbo.Ecare_Queue
            SET Status = 1
            WHERE CarteSlv      = @CarteSLV
              AND Matricule     = @PlaqueCamion
              AND Nom_Chaufeur  = @ChauffeurNom
              AND Status        = 0;";


        private const string UpdateFlux = @"
            UPDATE dbo.EcareFlux
            SET 
                BonDeCommande = @BonDeCommande,
                Quantity      = @Quantity,
                ClientName    = @ClientName,
                OrderId       = @OrderId
            WHERE 
                CarteSlv = @CarteSlv
                AND FirstWeight IS NULL
                AND ParkedAt >= DATEADD(HOUR, -24, CONVERT(datetime, SYSDATETIMEOFFSET() AT TIME ZONE 'Morocco Standard Time'));
        ";


        public async Task<Result<int>> Handle(CreateOrderFromFormCommand cmd, CancellationToken ct)
        {
            await uow.BeginAsync(ct);

            try
            {
                // 1) Insert Order
                var orderParams = new
                {
                    ShippingId = 1, // forced
                    cmd.NumeroCommande,
                    ChauffeurNom = cmd.ChauffeurNom,
                    PlaqueCamion = cmd.Matricule,
                    CarteSLV = cmd.CarteSLV,
                    Statut = "EnTraitement",
                    cmd.UserId,
                    NomComplet = cmd.ClientName
                };

                var newOrderId = await uow.Connection.ExecuteScalarAsync<int>(
                    new CommandDefinition(
                        InsertOrderSql,
                        orderParams,
                        uow.Transaction,
                        cancellationToken: ct));

                // 2) Insert OrderItem if provided
                if (cmd.ProductId.HasValue && cmd.QuantityT.HasValue)
                {
                    var itemParams = new
                    {
                        OrderId = newOrderId,
                        ProductId = cmd.ProductId.Value,
                        Quantity = cmd.QuantityT.Value,
                        Unite = "Tonnes"
                    };

                    await uow.Connection.ExecuteAsync(
                        new CommandDefinition(
                            InsertOrderItemSql,
                            itemParams,
                            uow.Transaction,
                            cancellationToken: ct));
                }

                // 3) Update Queue
                var queueParams = new
                {
                    CarteSLV = cmd.CarteSLV,
                    PlaqueCamion = cmd.Matricule,
                    ChauffeurNom = cmd.ChauffeurNom   // <-- matches @ChauffeurNom in SQL
                };

                await uow.Connection.ExecuteAsync(
                    new CommandDefinition(
                        UpdateQueueSql,
                        queueParams,
                        uow.Transaction,
                        cancellationToken: ct));

                // 4) Update Flux

                var fluxparams = new
                {
                    BonDeCommande = cmd.NumeroCommande,
                    Quantity = cmd.QuantityT,
                    ClientName = cmd.ClientName,
                    OrderId = newOrderId,
                    CarteSlv = cmd.CarteSLV
                };

                await uow.Connection.ExecuteAsync(
                   new CommandDefinition(
                       UpdateFlux,
                       fluxparams,
                       uow.Transaction,
                       cancellationToken: ct));

                await uow.CommitAsync(ct);
                return Result<int>.Ok(newOrderId);
            }
            catch
            {
                try { await uow.RollbackAsync(ct); } catch { }
                throw;
            }
        }
    }
}
