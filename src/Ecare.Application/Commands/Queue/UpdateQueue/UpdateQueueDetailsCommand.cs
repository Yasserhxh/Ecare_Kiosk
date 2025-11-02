using Ecare.Shared;
using MediatR;

namespace Ecare.Application.Commands.Queue.UpdateQueue;

public sealed record UpdateQueueDetailsCommand(
    string Matricule,
    string? BonCommande,
    string? Qualite1,
    string? Qualite2,
    decimal? Quantite1,
    decimal? Quantite2,
    string? NomChauffeur
) : IRequest<Result<int>>;
