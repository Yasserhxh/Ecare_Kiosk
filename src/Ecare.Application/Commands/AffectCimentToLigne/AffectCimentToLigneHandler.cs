using Dapper;
using Ecare.Shared;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecare.Application.Commands.AffectCimentToLigne
{
    public sealed class AffectCimentToLigneHandler(
       IUnitOfWork uow,
       ILogger<AffectCimentToLigneHandler> log)
       : IRequestHandler<AffectCimentToLigneCommand, Result>
    {
        public async Task<Result> Handle(
            AffectCimentToLigneCommand request,
            CancellationToken ct)
        {
            try
            {
                await uow.BeginAsync(ct);

                var conn = uow.Connection
                           ?? throw new InvalidOperationException("UnitOfWork.Connection is null in AffectCimentToLigneHandler.");

                // 1) Infos ligne + zone + type de ciment
                //    ProductCount = nombre de produits déjà affectés à cette ligne
                const string sqlInfo = """
                SELECT
                    z.TypeActivite AS TypeActivite,
                    z.TypeOperation AS TypeOperation,
                    (SELECT COUNT(*)
                     FROM Ecare_LigneCiments lc
                     WHERE lc.LigneId = l.Id) AS ProductCount,
                    c.[Type] AS CimentType
                FROM Ecare_Ligne l
                JOIN Ecare_Zone_Chargement z
                    ON l.ZoneChargementId = z.Id
                JOIN EcareCiments c
                    ON c.Id = @CimentId
                WHERE l.Id = @LigneId;
                """;

                var info = await conn.QuerySingleOrDefaultAsync<LineInfo>(
                    new CommandDefinition(
                        sqlInfo,
                        new { request.LigneId, request.CimentId },
                        transaction: uow.Transaction,
                        cancellationToken: ct));

                if (info is null)
                {
                    await uow.RollbackAsync(ct);
                    return Result.Fail("Ligne introuvable.");
                }

                var typeActivite = (info.TypeActivite ?? string.Empty).Trim().ToUpperInvariant();
                var typeOperation = (info.TypeOperation ?? string.Empty).Trim().ToUpperInvariant();
                var cimentType = (info.CimentType ?? string.Empty).Trim().ToUpperInvariant();

                // Type "logique" de la ligne : on privilégie TypeActivite, sinon TypeOperation
                var lineType = !string.IsNullOrEmpty(typeActivite)
                    ? typeActivite
                    : typeOperation;

                // 2) Compatibilité stricte des types :
                //    - SAC ligne -> SAC produit
                //    - VRAC ligne -> VRAC produit
                //    - PAL ligne -> PAL produit
                bool compatible =
                    !string.IsNullOrEmpty(lineType) &&
                    !string.IsNullOrEmpty(cimentType) &&
                    cimentType == lineType;

                if (!compatible)
                {
                    await uow.RollbackAsync(ct);

                    var msg = $"Ce produit ne peut pas être affecté à cette ligne, " +
                              $"car le type du produit ({cimentType}) est incompatible " +
                              $"avec le type de la ligne ({lineType}).";

                    return Result.Fail(msg);
                }

                // 3) Si ce produit est déjà affecté à cette ligne -> rien à faire
                const string sqlAlready = """
                SELECT COUNT(1)
                FROM Ecare_LigneCiments
                WHERE LigneId = @LigneId
                  AND CimentId = @CimentId;
                """;

                var already = await conn.ExecuteScalarAsync<int>(
                    new CommandDefinition(
                        sqlAlready,
                        new { request.LigneId, request.CimentId },
                        transaction: uow.Transaction,
                        cancellationToken: ct));

                if (already > 0)
                {
                    await uow.RollbackAsync(ct);
                    return Result.Fail("Ce produit est déjà affecté à cette ligne.");
                }

                // === Règles de capacité ===
                // - SAC : max 1 produit -> INSERT si vide, sinon UPDATE
                // - VRAC : max 1 produit -> INSERT si vide, sinon UPDATE
                // - PAL : max 4 produits -> INSERT jusqu'à 4, puis blocage

                bool isSac = lineType == "SAC";
                bool isVrac = lineType == "VRAC";
                bool isPal = lineType == "PAL";

                // CAS SAC / VRAC => slot unique avec remplacement
                if (isSac || isVrac)
                {
                    // Chercher s'il existe déjà un lien pour cette ligne (quel que soit le CimentId)
                    const string sqlExistingSingle = """
                    SELECT TOP(1) CimentId
                    FROM Ecare_LigneCiments
                    WHERE LigneId = @LigneId;
                    """;

                    var existingCimentId = await conn.ExecuteScalarAsync<int?>(
                        new CommandDefinition(
                            sqlExistingSingle,
                            new { request.LigneId },
                            transaction: uow.Transaction,
                            cancellationToken: ct));

                    if (existingCimentId is null)
                    {
                        // Ligne vide -> INSÉRER le premier produit
                        const string sqlInsertSingle = """
                        INSERT INTO Ecare_LigneCiments (LigneId, CimentId)
                        VALUES (@LigneId, @CimentId);
                        """;

                        await conn.ExecuteAsync(
                            new CommandDefinition(
                                sqlInsertSingle,
                                new { request.LigneId, request.CimentId },
                                transaction: uow.Transaction,
                                cancellationToken: ct));
                    }
                    else
                    {
                        // Ligne a déjà un produit :
                        // - si c'est le même -> message
                        // - sinon -> MODIFIER le CimentId (UPDATE)
                        if (existingCimentId.Value == request.CimentId)
                        {
                            await uow.RollbackAsync(ct);
                            return Result.Fail("Ce produit est déjà affecté à cette ligne.");
                        }

                        const string sqlUpdateSingle = """
                        UPDATE Ecare_LigneCiments
                        SET CimentId = @CimentId
                        WHERE LigneId = @LigneId;
                        """;

                        await conn.ExecuteAsync(
                            new CommandDefinition(
                                sqlUpdateSingle,
                                new { request.LigneId, request.CimentId },
                                transaction: uow.Transaction,
                                cancellationToken: ct));
                    }

                    await uow.CommitAsync(ct);
                    return Result.Ok();
                }

                // CAS PAL => max 4 produits
                if (isPal)
                {
                    if (info.ProductCount >= 4)
                    {
                        await uow.RollbackAsync(ct);
                        return Result.Fail("Cette ligne PAL ne peut pas être associée à plus de 4 produits.");
                    }

                    const string sqlInsertPal = """
                    INSERT INTO Ecare_LigneCiments (LigneId, CimentId)
                    VALUES (@LigneId, @CimentId);
                    """;

                    await conn.ExecuteAsync(
                        new CommandDefinition(
                            sqlInsertPal,
                            new { request.LigneId, request.CimentId },
                            transaction: uow.Transaction,
                            cancellationToken: ct));

                    await uow.CommitAsync(ct);
                    return Result.Ok();
                }

                // AUTRES TYPES (si jamais il y en a) : on fait simple, un INSERT sans limite
                const string sqlInsertOther = """
                INSERT INTO Ecare_LigneCiments (LigneId, CimentId)
                VALUES (@LigneId, @CimentId);
                """;

                await conn.ExecuteAsync(
                    new CommandDefinition(
                        sqlInsertOther,
                        new { request.LigneId, request.CimentId },
                        transaction: uow.Transaction,
                        cancellationToken: ct));

                await uow.CommitAsync(ct);
                return Result.Ok();
            }
            catch (Exception ex)
            {
                log.LogError(ex,
                    "Erreur lors de l'affectation du ciment {CimentId} à la ligne {LigneId}",
                    request.CimentId, request.LigneId);

                await uow.RollbackAsync(ct);
                return Result.Fail("Erreur technique lors de l'affectation du produit à la ligne.");
            }
        }

        private sealed class LineInfo
        {
            public string? TypeActivite { get; init; }
            public string? TypeOperation { get; init; }
            public int ProductCount { get; init; }
            public string? CimentType { get; init; }
        }
    }
}
