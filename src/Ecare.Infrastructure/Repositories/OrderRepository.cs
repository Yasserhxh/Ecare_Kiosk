using Dapper;
using Ecare.Domain.Entities;
using Ecare.Domain;
using Ecare.Shared;

namespace Ecare.Infrastructure.Repositories;

public interface IOrderRepository
{
    Task<Order?> GetByNumberAsync(string number, IUnitOfWork uow);
    Task<Order?> GetBySlvAsync(string slvCard, IUnitOfWork uow);
    Task UpdateStatusAsync(int id, OrderStatus status, IUnitOfWork uow);
}

public sealed class OrderRepository : IOrderRepository
{
    public Task<Order?> GetByNumberAsync(string number, IUnitOfWork uow)
        => uow.Connection.QuerySingleOrDefaultAsync<Order>(
            $@"SELECT TOP(1)
                    Id,
                    ShippingId,
                    NumeroCommande,
                    DateCommande,
                    Destination,
                    ModeDelivraison,
                    EmplacementChargement,
                    MethodeChargement,
                    HeureEnlevement,
                    HeureDelivraison,
                    CarteSLV,
                    ChauffeurNom,
                    ChauffeurPrenom,
                    PermisDeConduire,
                    PlaqueCamion,
                    Statut,
                    UserId,
                    Commentaire,
                    NomComplet
                FROM {DbTableNames.Orders}
                WHERE NumeroCommande=@number",
            new { number },
            uow.Transaction);

    public Task<Order?> GetBySlvAsync(string slvCard, IUnitOfWork uow)
        => uow.Connection.QuerySingleOrDefaultAsync<Order>(
            $@"SELECT TOP(1)
                    Id,
                    ShippingId,
                    NumeroCommande,
                    DateCommande,
                    Destination,
                    ModeDelivraison,
                    EmplacementChargement,
                    MethodeChargement,
                    HeureEnlevement,
                    HeureDelivraison,
                    CarteSLV,
                    ChauffeurNom,
                    ChauffeurPrenom,
                    PermisDeConduire,
                    PlaqueCamion,
                    Statut,
                    UserId,
                    Commentaire,
                    NomComplet
                FROM {DbTableNames.Orders}
                WHERE CarteSLV=@slvCard AND Statut<>@cancelled
                ORDER BY DateCommande DESC",
            new { slvCard, cancelled = "Annulee" },
            uow.Transaction);

    public Task UpdateStatusAsync(int id, OrderStatus status, IUnitOfWork uow)
    {
        var statusString = status switch
        {
            OrderStatus.Cree => "Crée",
            OrderStatus.Confirmee => "Confirmée",
            OrderStatus.EnTraitement => "EnTraitement",
            OrderStatus.Livre => "Livre",
            OrderStatus.Termine => "Termine",
            OrderStatus.Annulee => "Annulee",
            _ => "Crée"
        };

        return uow.Connection.ExecuteAsync(
            $"UPDATE {DbTableNames.Orders} SET Statut=@status WHERE Id=@id",
            new { id, status = statusString },
            uow.Transaction);
    }
}
