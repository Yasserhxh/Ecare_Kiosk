using Dapper;
using Ecare.Shared;

namespace Ecare.Infrastructure.Repositories;

public interface ILegacyOrderWriter
{
    Task<(int OrderId, string NumeroCommande)?> CreateOrderWithItemAsync(
        string slv,
        string truckPlate,
        int productId,
        decimal quantity,
        string unit,
        IUnitOfWork uow,
        CancellationToken ct);
}

public sealed class LegacyOrderWriter : ILegacyOrderWriter
{
    public async Task<(int OrderId, string NumeroCommande)?> CreateOrderWithItemAsync(
        string slv,
        string truckPlate,
        int productId,
        decimal quantity,
        string unit,
        IUnitOfWork uow,
        CancellationToken ct)
    {
        var numero = $"CMD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpper()}";

        // Get or create a default shipping ID
        var getShippingId = new CommandDefinition(
            "SELECT TOP 1 Id FROM dbo.Shippings ORDER BY Id",
            transaction: uow.Transaction,
            cancellationToken: ct);
        var shippingId = await uow.Connection.ExecuteScalarAsync<int?>(getShippingId);

        // If no shipping exists, create a default one
        if (shippingId is null)
        {
            var createShipping = new CommandDefinition(
                "INSERT INTO dbo.Shippings (UserId, DateCreation) VALUES ('kiosk', SYSUTCDATETIME()); SELECT CAST(SCOPE_IDENTITY() as int);",
                transaction: uow.Transaction,
                cancellationToken: ct);
            shippingId = await uow.Connection.ExecuteScalarAsync<int?>(createShipping);
        }

        // Insert into dbo.Orders (minimal required fields per schema: ShippingId, NumeroCommande, DateCommande, CarteSLV, PlaqueCamion, Statut, UserId)
        var insertOrder = new CommandDefinition(
            $@"INSERT INTO [dbo].[Orders] (ShippingId, NumeroCommande, DateCommande, CarteSLV, PlaqueCamion, Statut, UserId)
               VALUES (@ShippingId, @NumeroCommande, SYSUTCDATETIME(), @CarteSLV, @PlaqueCamion, @Statut, @UserId);
               SELECT CAST(SCOPE_IDENTITY() as int);",
            new
            {
                ShippingId = shippingId ?? 1, // Use found shipping ID or default to 1
                NumeroCommande = numero,
                CarteSLV = slv,
                PlaqueCamion = truckPlate,
                Statut = "Crée",
                UserId = "kiosk"
            },
            transaction: uow.Transaction,
            cancellationToken: ct);

        var orderId = await uow.Connection.ExecuteScalarAsync<int?>(insertOrder);
        if (orderId is null) return null;

        var insertItem = new CommandDefinition(
            $"INSERT INTO [dbo].[Ecare_OrderItems] (OrderId, ProductId, Quantity, Unite) VALUES (@OrderId,@ProductId,@Quantity,@Unite)",
            new { OrderId = orderId.Value, ProductId = productId, Quantity = quantity, Unite = unit },
            transaction: uow.Transaction,
            cancellationToken: ct);
        var rows = await uow.Connection.ExecuteAsync(insertItem);
        return rows == 1 ? (orderId.Value, numero) : null;
    }
}


