using Ecare.Domain.ValueObjects;
using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries
{
    // Return grouped buckets:
    // - One bucket named "En Validation" for Status = Pending(0)
    // - Buckets per Qualite1 for Status = InProgress(1)
    // Items in every bucket are ordered by CreatedAt (oldest -> newest)
    public sealed record GetActiveQueueEntriesQuery
        : IRequest<Result<IReadOnlyList<GetActiveQueueEntriesQuery.Group>>>
    {
        public sealed record Item(
            string? Matricule,
            string? Qualite1,
            QueueStatus Status,
            DateTime CreatedAt
        );

        public sealed record Group(
            string Name,
            IReadOnlyList<Item> Items
        );
    }
}
