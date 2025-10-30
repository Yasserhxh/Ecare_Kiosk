using Ecare.Shared;
using MediatR;

namespace Ecare.Application.Commands.Flux;

public sealed record CreateFluxEntryCommand(
    string? BonDeCommande,
    int? Quantity,
    string? Matricule,
    string? DriverName,
    string? ClientName,
    DateTime ParkedAt,
    int? OrderId           
) : IRequest<Result<int>>;
