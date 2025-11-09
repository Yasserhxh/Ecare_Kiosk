using MediatR;
using Ecare.Shared;

namespace Ecare.Application.Commands.Orders;

public sealed record CreateOrderFromFormCommand(
    string Matricule,           // form: Matricule
    string? ChauffeurNom,       // form: Chauffeur (nullable)
    string ClientName,          // form: Client
    string NumeroCommande,      // form: Bon de commande
    int? ProductId,             // form: Produit (optional)
    decimal? QuantityT,         // form: Quantité (T) (optional)
    string UserId,
    int CarteSLV
) : IRequest<Result<int>>;
