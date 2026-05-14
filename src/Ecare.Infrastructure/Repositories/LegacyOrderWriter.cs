using Dapper;
using Ecare.Shared;

namespace Ecare.Infrastructure.Repositories;

public interface ILegacyOrderWriter
{
    Task<(int OrderId, string NumeroCommande)?> CreateOrderWithItemAsync(
        string NumeroCommande,
        string slv,
        string truckPlate,
        int productId,
        int? productId2 = null,
        decimal quantity = 0,
        decimal? quantity2 = null,
        string unit = "T",
        string? unit2 = null,
        IUnitOfWork? uow = null,
        CancellationToken ct = default);
}


public sealed class LegacyOrderWriter : ILegacyOrderWriter
{
    public async Task<(int OrderId, string NumeroCommande)?> CreateOrderWithItemAsync(
        string numeroCommande,
        string slv,
        string truckPlate,
        int productId,
        int? productId2 = null,
        decimal quantity = 0,
        decimal? quantity2 = null,
        string unit = "T",
        string? unit2 = null,
        IUnitOfWork? uow = null,
        CancellationToken ct = default)
    {
        //var numero = $"CMD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpper()}";

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
                "INSERT INTO dbo.Shippings (UserId, DateCreation) VALUES ('kiosk', CONVERT(datetime, SYSDATETIMEOFFSET() AT TIME ZONE 'Morocco Standard Time')); SELECT CAST(SCOPE_IDENTITY() as int);",
                transaction: uow.Transaction,
                cancellationToken: ct);
            shippingId = await uow.Connection.ExecuteScalarAsync<int?>(createShipping);
        }

        // Insert into dbo.Orders (minimal required fields per schema: ShippingId, NumeroCommande, DateCommande, CarteSLV, PlaqueCamion, Statut, UserId)
        var insertOrder = new CommandDefinition(
            $@"INSERT INTO [dbo].[Orders] (ShippingId, NumeroCommande, DateCommande, CarteSLV, PlaqueCamion, Statut, UserId)
               VALUES (@ShippingId, @NumeroCommande, CONVERT(datetime, SYSDATETIMEOFFSET() AT TIME ZONE 'Morocco Standard Time'), @CarteSLV, @PlaqueCamion, @Statut, @UserId);
               SELECT CAST(SCOPE_IDENTITY() as int);",
            new
            {
                ShippingId = shippingId ?? 1, // Use found shipping ID or default to 1
                NumeroCommande = numeroCommande,
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

        if (productId2 != null && productId2 != 0)
        {
            var insertItem2 = new CommandDefinition(
            $"INSERT INTO [dbo].[Ecare_OrderItems] (OrderId, ProductId, Quantity, Unite) VALUES (@OrderId,@ProductId,@Quantity,@Unite)",
            new { OrderId = orderId.Value, ProductId = productId2, Quantity = quantity2, Unite = unit2 },
            transaction: uow.Transaction,
            cancellationToken: ct);
            var rows2 = await uow.Connection.ExecuteAsync(insertItem2);
        }
        return rows == 1 ? (orderId.Value,numeroCommande) : null;
    }
}


