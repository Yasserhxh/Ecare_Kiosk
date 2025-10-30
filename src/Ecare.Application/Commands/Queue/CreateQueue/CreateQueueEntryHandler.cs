// Ecare.Application/Commands/Queue/CreateQueue/CreateQueueEntryHandler.cs
using Dapper;
using Ecare.Application.Services.Queue;
using Ecare.Domain.ValueObjects;               // QueueStatus enum
using Ecare.Shared;
using MediatR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Logging;

namespace Ecare.Application.Commands.Queue.CreateQueue;

public sealed class CreateQueueEntryHandler(
    IUnitOfWork uow,
    ServiceManager signalR,
    ILogger<CreateQueueEntryHandler> log)
    : IRequestHandler<CreateQueueEntryCommand, Result<int>>
{
    public async Task<Result<int>> Handle(CreateQueueEntryCommand request, CancellationToken ct)
    {
        await uow.BeginAsync(ct);
        try
        {
            // 1) Insert new row (not pinned)
            var newId = await EcareQueueWriter.InsertAsync(
                uow,
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

            await uow.CommitAsync(ct);

            // 2) Rebuild + broadcast snapshot via shared helper
            await QueueSnapshot.BuildAndBroadcastAsync(signalR, uow, log, ct);

            log.LogInformation("Queue entry {Id} inserted and broadcast sent.", newId);
            return Result<int>.Ok(newId);
        }
        catch (Exception ex)
        {
            try { await uow.RollbackAsync(ct); } catch { }
            log.LogError(ex, "Failed to insert queue entry");
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
        p.Add("Matricule", matricule, System.Data.DbType.String);
        p.Add("Nom_Chaufeur", nomChauffeur, System.Data.DbType.String);
        p.Add("Qualite1", qualite1, System.Data.DbType.String);
        p.Add("Qualite2", qualite2, System.Data.DbType.String);
        p.Add("Quantite1", quantite1, System.Data.DbType.Decimal);
        p.Add("Quantite2", quantite2, System.Data.DbType.Decimal);
        p.Add("Bon_Commande", bonCommande, System.Data.DbType.String);
        p.Add("Bon_Livraison", bonLivraison, System.Data.DbType.String);
        p.Add("Source", source, System.Data.DbType.String);
        p.Add("Status", status, System.Data.DbType.Int32);
        p.Add("CreatedAt", createdAt, System.Data.DbType.DateTime2);
        p.Add("IsPined", isPined, System.Data.DbType.Boolean);   // BIT
        p.Add("PinedAt", pinedAt, System.Data.DbType.DateTime2); // nullable

        return await uow.Connection.ExecuteScalarAsync<int>(
            new CommandDefinition(InsertSql, p, uow.Transaction, cancellationToken: ct));
    }
}
