using Ecare.Domain.ValueObjects;
using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.Queue.CreateQueue
{
    public sealed record CreateQueueEntryCommand(
        string? Matricule,
        string? NomChauffeur,
        string? Qualite1,
        string? Qualite2,
        decimal? Quantite1,
        decimal? Quantite2,
        string? BonCommande,
        string? BonLivraison,
        string? Source,
        QueueStatus Status,
        DateTime CreatedAt
    ) : IRequest<Result<int>>;
}
