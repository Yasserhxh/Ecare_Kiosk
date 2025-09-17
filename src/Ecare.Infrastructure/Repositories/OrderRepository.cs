using Dapper;
using Ecare.Domain.Entities;
using Ecare.Domain;
using Ecare.Shared;

namespace Ecare.Infrastructure.Repositories;

public interface IOrderRepository
{
    Task<Order?> GetByNumberAsync(string number, IUnitOfWork uow);
    Task<Order?> GetBySlvAsync(string slvCard, IUnitOfWork uow);
    Task UpdateStatusAsync(Guid id, OrderStatus status, IUnitOfWork uow);
}

public sealed class OrderRepository : IOrderRepository
{
    public Task<Order?> GetByNumberAsync(string number, IUnitOfWork uow)
        => uow.Connection.QuerySingleOrDefaultAsync<Order>(
            $@"SELECT TOP(1)
                    Id,
                    ShippingId,
                    NumeroCommande     AS Number,
                    DateCommande       AS OrderedAt,
                    Destination,
                    ModeDelivraison    AS DeliveryMode,
                    EmplacementChargement AS LoadingLocation,
                    MethodeChargement  AS LoadingMethod,
                    HeureEnlevement    AS PickupTime,
                    HeureDelivraison   AS DeliveryTime,
                    CarteSLV           AS SlvCard,
                    ChauffeurNom       AS DriverLastName,
                    ChauffeurPrenom    AS DriverFirstName,
                    PermisDeConduire   AS DriverLicense,
                    PlaqueCamion       AS TruckPlate,
                    Statut             AS Status,
                    UserId,
                    CAST(NULL AS decimal(18,3)) AS TargetQuantityTons
                FROM {DbTableNames.Orders}
                WHERE NumeroCommande=@number",
            new { number },
            uow.Transaction);

    public Task<Order?> GetBySlvAsync(string slvCard, IUnitOfWork uow)
        => uow.Connection.QuerySingleOrDefaultAsync<Order>(
            $@"SELECT TOP(1)
                    Id,
                    ShippingId,
                    NumeroCommande     AS Number,
                    DateCommande       AS OrderedAt,
                    Destination,
                    ModeDelivraison    AS DeliveryMode,
                    EmplacementChargement AS LoadingLocation,
                    MethodeChargement  AS LoadingMethod,
                    HeureEnlevement    AS PickupTime,
                    HeureDelivraison   AS DeliveryTime,
                    CarteSLV           AS SlvCard,
                    ChauffeurNom       AS DriverLastName,
                    ChauffeurPrenom    AS DriverFirstName,
                    PermisDeConduire   AS DriverLicense,
                    PlaqueCamion       AS TruckPlate,
                    Statut             AS Status,
                    UserId,
                    CAST(NULL AS decimal(18,3)) AS TargetQuantityTons
                FROM {DbTableNames.Orders}
                WHERE CarteSLV=@slvCard AND Statut<>@cancelled
                ORDER BY DateCommande DESC",
            new { slvCard, cancelled = OrderStatus.Annulee },
            uow.Transaction);

    public Task UpdateStatusAsync(Guid id, OrderStatus status, IUnitOfWork uow)
        => uow.Connection.ExecuteAsync(
            $"UPDATE {DbTableNames.Orders} SET Statut=@status WHERE Id=@id",
            new { id, status },
            uow.Transaction);
}
