using Dapper;
using Ecare.Domain.Interfaces;   // IUnitOfWork
using Ecare.Domain.ValueObjects; // QueueStatus
using System.Data;
using Ecare.Shared;

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

    /// <summary>
    /// Inserts a row into Ecare_Queue and returns the new Id.
    /// All text params can be null/empty; decimals are nullable.
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

        // Dapper doesn't take CancellationToken on ExecuteScalarAsync; wrap if you need hard cancel.
        var newId = await uow.Connection.ExecuteScalarAsync<int>(
            InsertSql, p, uow.Transaction);

        return newId;
    }
}
