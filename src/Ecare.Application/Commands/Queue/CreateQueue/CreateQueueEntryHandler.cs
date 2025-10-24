using Dapper;
using Ecare.Application.Services;
using Ecare.Domain.ValueObjects;
using Ecare.Shared;
using MediatR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Linq;

namespace Ecare.Application.Commands.Queue.CreateQueue
{
    public sealed class CreateQueueEntryHandler : IRequestHandler<CreateQueueEntryCommand, Result<int>>
    {
        private readonly IUnitOfWork _uow;
        private readonly ServiceManager _signalR;
        private readonly ILogger<CreateQueueEntryHandler> _log;

        public CreateQueueEntryHandler(
            IUnitOfWork uow,
            ServiceManager signalR,
            ILogger<CreateQueueEntryHandler> log)
        {
            _uow = uow;
            _signalR = signalR;
            _log = log;
        }

        private const string SelectSql = @"
            SELECT Matricule, Qualite1, Status, CreatedAt
            FROM Ecare_Queue
            WHERE Status BETWEEN 0 AND 1
            ORDER BY CreatedAt ASC;"; // oldest → newest

        public async Task<Result<int>> Handle(CreateQueueEntryCommand request, CancellationToken ct)
        {
            await _uow.BeginAsync(ct);
            try
            {
                //Insert new queue entry
                var newId = await EcareQueueWriter.InsertAsync(
                    _uow,
                    request.Matricule,
                    request.NomChauffeur,
                    request.Qualite1,
                    request.Qualite2,
                    request.Quantite1,
                    request.Quantite2,
                    request.BonCommande,
                    request.BonLivraison,
                    request.Source,
                    request.Status,
                    request.CreatedAt,
                    ct: ct);

                await _uow.CommitAsync(ct);

                //Build grouped snapshot
                var grouped = await BuildGroupedSnapshotAsync(ct);

                //Broadcast grouped snapshot to SignalR clients
                await SignalRHelper.BroadcastAsync(
                    _signalR,
                    hubName: "queue_data_hub",
                    methodName: "QueueDataEvent",
                    payload: grouped,
                    ct: ct,
                    logger: _log
                );

                _log.LogInformation("Queue entry {id} inserted and broadcast sent.", newId);
                return Result<int>.Ok(newId);
            }
            catch (Exception ex)
            {
                await _uow.RollbackAsync(ct);
                _log.LogError(ex, "Failed to insert queue entry");
                throw;
            }
        }

        // -------- Helpers --------

        private sealed record QueueItem(
            string? Matricule,
            string? Qualite1,
            QueueStatus Status,
            DateTime CreatedAt);

        private sealed record QueueGroup(
            string Name,
            IReadOnlyList<QueueItem> Items);

        private async Task<IReadOnlyList<QueueGroup>> BuildGroupedSnapshotAsync(CancellationToken ct)
        {
            await _uow.BeginAsync(ct);
            try
            {
                var items = (await _uow.Connection.QueryAsync<QueueItem>(
                    SelectSql, transaction: _uow.Transaction)).ToList();

                await _uow.CommitAsync(ct);

                // Status = 0 → "En Validation"
                var enValidation = items
                    .Where(i => i.Status == QueueStatus.EnValidation)
                    .OrderBy(i => i.CreatedAt)
                    .ToList();

                // Status = 1 → group by Qualite1
                var inProgressGroups = items
                    .Where(i => i.Status == QueueStatus.EncourTraitement)
                    .GroupBy(i => string.IsNullOrWhiteSpace(i.Qualite1)
                        ? "(Sans Qualité)"
                        : i.Qualite1!)
                    .OrderBy(g => g.Key)
                    .Select(g => new QueueGroup(
                        Name: g.Key,
                        Items: g.OrderBy(i => i.CreatedAt).ToList()))
                    .ToList();

                var result = new List<QueueGroup>(1 + inProgressGroups.Count);
                if (enValidation.Count > 0)
                    result.Add(new QueueGroup("En Validation", enValidation));

                result.AddRange(inProgressGroups);
                return result;
            }
            catch (Exception ex)
            {
                await _uow.RollbackAsync(ct);
                _log.LogError(ex, "❌ Failed to build queue snapshot");
                throw;
            }
        }
    }

    // -------- Writer --------
    public static class EcareQueueWriter
    {
        private const string InsertSql = @"
            INSERT INTO Ecare_Queue
            (
                Matricule,
                Nom_Chaufeur,
                Qualite1,
                Qualite2,
                Quantite1,
                Quantite2,
                Bon_Commande,
                Bon_Livraison,
                Source,
                Status,
                CreatedAt
            )
            OUTPUT INSERTED.Id
            VALUES
            (
                @Matricule,
                @Nom_Chaufeur,
                @Qualite1,
                @Qualite2,
                @Quantite1,
                @Quantite2,
                @Bon_Commande,
                @Bon_Livraison,
                @Source,
                @Status,
                @CreatedAt
            );";

        public static async Task<int> InsertAsync(
            IUnitOfWork uow,
            string? matricule,
            string? nomChauffeur,
            string? qualite1,
            string? qualite2,
            decimal? quantite1,
            decimal? quantite2,
            string? bonCommande,
            string? bonLivraison,
            string? source,
            QueueStatus status,
            DateTime createdAt,
            CancellationToken ct = default)
        {
            var p = new DynamicParameters();
            p.Add("Matricule", matricule, DbType.String);
            p.Add("Nom_Chaufeur", nomChauffeur, DbType.String);
            p.Add("Qualite1", qualite1, DbType.String);
            p.Add("Qualite2", qualite2, DbType.String);
            p.Add("Quantite1", quantite1, DbType.Decimal);
            p.Add("Quantite2", quantite2, DbType.Decimal);
            p.Add("Bon_Commande", bonCommande, DbType.String);
            p.Add("Bon_Livraison", bonLivraison, DbType.String);
            p.Add("Source", source, DbType.String);
            p.Add("Status", status, DbType.Int32);
            p.Add("CreatedAt", createdAt, DbType.DateTime);

            return await uow.Connection.ExecuteScalarAsync<int>(
                InsertSql, p, uow.Transaction);
        }
    }
}
