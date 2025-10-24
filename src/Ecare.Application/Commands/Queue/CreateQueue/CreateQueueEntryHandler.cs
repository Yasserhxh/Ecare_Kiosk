// Ecare.Application/Commands/Queue/CreateQueue/CreateQueueEntryHandler.cs
using Dapper;
using Ecare.Application.Services;
using Ecare.Domain.ValueObjects; // QueueStatus enum (EnValidation = 0, EncourTraitement = 1)
using Ecare.Shared;
using MediatR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Logging;
using System.Data;

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

        // Include IsPined/PinedAt for proper ordering (even if En Validation ignores them)
        private const string SelectSql = @"
            SELECT 
                Matricule,
                Qualite1,
                Status,
                CreatedAt,
                IsPined,
                PinedAt
            FROM dbo.Ecare_Queue
            WHERE Status BETWEEN 0 AND 1
            ORDER BY CreatedAt ASC;";

        public async Task<Result<int>> Handle(CreateQueueEntryCommand request, CancellationToken ct)
        {
            await _uow.BeginAsync(ct);
            try
            {
                // Insert new queue row (initially not pinned)
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
                    isPined: false,
                    pinedAt: null,
                    ct: ct);

                await _uow.CommitAsync(ct);

                // Build grouped snapshot (pin ignored in En Validation)
                var grouped = await BuildGroupedSnapshotAsync(ct);

                // Broadcast snapshot to all clients
                await SignalRHelper.BroadcastAsync(
                    _signalR,
                    hubName: "queue_data_hub",
                    methodName: "QueueDataEvent",
                    payload: grouped,
                    ct: ct,
                    logger: _log
                );

                _log.LogInformation("Queue entry {Id} inserted and broadcast sent.", newId);
                return Result<int>.Ok(newId);
            }
            catch (Exception ex)
            {
                try { await _uow.RollbackAsync(ct); } catch { }
                _log.LogError(ex, "Failed to insert queue entry");
                throw;
            }
        }

        // -------- Snapshot models --------

        private sealed record QueueItem(
            string? Matricule,
            string? Qualite1,
            QueueStatus Status,
            DateTime CreatedAt,
            bool IsPined,
            DateTime? PinedAt);

        private sealed record QueueGroup(
            string Name,
            IReadOnlyList<QueueItem> Items);

        // -------- Build grouped snapshot --------
        private async Task<IReadOnlyList<QueueGroup>> BuildGroupedSnapshotAsync(CancellationToken ct)
        {
            await _uow.BeginAsync(ct);
            try
            {
                var items = (await _uow.Connection.QueryAsync<QueueItem>(
                    new CommandDefinition(SelectSql, transaction: _uow.Transaction, cancellationToken: ct)
                )).ToList();

                await _uow.CommitAsync(ct);

                // Lane 1: En Validation (Status = 0) — IGNORE pinning, FIFO by CreatedAt
                var enValidation = items
                    .Where(i => i.Status == QueueStatus.EnValidation)
                    .OrderBy(i => i.CreatedAt)
                    .ToList();

                // Lane 2: En cours de traitement (Status = 1)
                // Group by Qualite1, each group pinned-first then newest pin, then FIFO
                var inProgressGroups = items
                    .Where(i => i.Status == QueueStatus.EncourTraitement)
                    .GroupBy(i => string.IsNullOrWhiteSpace(i.Qualite1) ? "(Sans Qualité)" : i.Qualite1!)
                    .OrderBy(g => g.Key)
                    .Select(g => new QueueGroup(
                        Name: g.Key,
                        Items: g
                            .OrderByDescending(i => i.IsPined)                       // pinned first
                            .ThenByDescending(i => i.PinedAt ?? DateTime.MinValue)   // recent pins near top
                            .ThenBy(i => i.CreatedAt)                                // FIFO among same pin state
                            .ToList()))
                    .ToList();

                var result = new List<QueueGroup>(1 + inProgressGroups.Count);
                if (enValidation.Count > 0)
                    result.Add(new QueueGroup("En Validation", enValidation));

                result.AddRange(inProgressGroups);
                return result;
            }
            catch
            {
                try { await _uow.RollbackAsync(ct); } catch { }
                throw;
            }
        }
    }

    // -------- Writer (INSERT) --------
    public static class EcareQueueWriter
    {
        private const string InsertSql = @"
            INSERT INTO dbo.Ecare_Queue
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
                CreatedAt,
                IsPined,
                PinedAt
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
                @CreatedAt,
                @IsPined,
                @PinedAt
            );";

        /// <summary>
        /// Inserts a row into Ecare_Queue and returns the new Id.
        /// </summary>
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
            bool isPined,
            DateTime? pinedAt,
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
            p.Add("CreatedAt", createdAt, DbType.DateTime2);
            p.Add("IsPined", isPined, DbType.Boolean);   // BIT
            p.Add("PinedAt", pinedAt, DbType.DateTime2); // nullable

            return await uow.Connection.ExecuteScalarAsync<int>(
                new CommandDefinition(InsertSql, p, uow.Transaction, cancellationToken: ct));
        }
    }
}
