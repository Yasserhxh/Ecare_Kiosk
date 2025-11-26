using Ecare.Domain.ValueObjects;
using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;

namespace Ecare.Application.Queries
{
    public sealed class GetActiveQueueEntriesHandler(IUnitOfWork uow)
        : IRequestHandler<GetActiveQueueEntriesQuery, Result<IReadOnlyList<GetActiveQueueEntriesQuery.Group>>>
    {
        private const string Sql = @"
SELECT Matricule, Qualite1, Status, CreatedAt
FROM Ecare_Queue
WHERE Status BETWEEN 0 AND 1
ORDER BY CreatedAt ASC;";

        public async Task<Result<IReadOnlyList<GetActiveQueueEntriesQuery.Group>>> Handle(
            GetActiveQueueEntriesQuery request, CancellationToken ct)
        {
            await uow.BeginAsync(ct); // <-- ensure Connection is created/usable
            try
            {
                var items = (await uow.Connection.QueryAsync<GetActiveQueueEntriesQuery.Item>(
                    Sql, transaction: uow.Transaction)).ToList();

                // Status = 0 → "En Validation"
                var enValidationItems = items
                    .Where(i => i.Status == QueueStatus.EnValidation)
                    .OrderBy(i => i.CreatedAt)
                    .ToList();

                // Status = 1 → group by Qualite1
                var inProgressGroups = items
                    .Where(i => i.Status == QueueStatus.EncourTraitement)
                    .GroupBy(i => string.IsNullOrWhiteSpace(i.Qualite1) ? "(Sans Qualité)" : i.Qualite1!)
                    .OrderBy(g => g.Key)
                    .Select(g => new GetActiveQueueEntriesQuery.Group(
                        Name: g.Key,
                        Items: g.OrderBy(i => i.CreatedAt).ToList()
                    ))
                    .ToList();

                var resultGroups = new List<GetActiveQueueEntriesQuery.Group>(1 + inProgressGroups.Count);
                if (enValidationItems.Count > 0)
                {
                    resultGroups.Add(new GetActiveQueueEntriesQuery.Group(
                        Name: "En Validation",
                        Items: enValidationItems));
                }

                resultGroups.AddRange(inProgressGroups);

                await uow.CommitAsync(ct); // clean close
                return Result<IReadOnlyList<GetActiveQueueEntriesQuery.Group>>.Ok(resultGroups);
            }
            catch
            {
                await uow.RollbackAsync(ct);
                throw;
            }
        }
    }
}
