using Ecare.Domain.ValueObjects;
using Ecare.Shared;
using MediatR;

namespace Ecare.Application.Engines.Create;

public sealed record CreateEcareEngineCommand(
    string Matricule,
    int? PTAC,
    int? TARE,
    int? Type_Camion,
    int? CarteSlv,
    ProcessType? Type_Process,
    int? Code_Process,
    string? Nom_Process,
    int? Code_Chantier,
    string? Nom_Chantier,
    int? Id_Chauffeur
) : IRequest<Result<string>>;
