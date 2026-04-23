using Ecare.Shared;
using MediatR;

namespace Ecare.Application.Commands.EcareDriver;

public sealed record UpdateDriverCommand(
    int Id,
    string Cin,
    string Nom,
    string Prenom,
    string Numero,
    string? Permis = null,
    string? NomComplet = null
) : IRequest<Result<bool>>;
