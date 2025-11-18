using Ecare.Shared;
using MediatR;

namespace Ecare.Application.Commands.Legend;

public sealed record UpdateAfterFirstWeightCommand(
    int RfidCard,
    string Matricule,
    int PremierePoid,
    string Produit1
) : IRequest<Result<bool>>;
