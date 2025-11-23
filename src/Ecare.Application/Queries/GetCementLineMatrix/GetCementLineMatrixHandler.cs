using Dapper;
using Ecare.Domain.ValueObjects;
using Ecare.Shared;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecare.Application.Queries.GetCementLineMatrix
{
    public sealed class GetCementLineMatrixHandler(
        IUnitOfWork uow,
        ILogger<GetCementLineMatrixHandler> log)
        : IRequestHandler<GetCementLineMatrixQuery, Result<CementMatrixVm>>
    {
        public async Task<Result<CementMatrixVm>> Handle(
            GetCementLineMatrixQuery request,
            CancellationToken ct)
        {
            const string sql = """
            -- 1) Lignes + affectations
            SELECT
                l.Id            AS LigneId,
                l.Nom           AS LineName,
                l.Status        AS Status,
                l.Capacity      AS Capacity,
                z.Usine         AS Usine,
                z.TypeActivite  AS TypeActivite,
                z.TypeOperation AS TypeOperation,
                c.Id            AS CimentId,
                c.Name          AS CimentName,
                c.[Type]        AS CimentType,
                lc.Actif        AS ActifProduct
            FROM Ecare_Ligne l
            JOIN Ecare_Zone_Chargement z
                ON l.ZoneChargementId = z.Id
            LEFT JOIN Ecare_LigneCiments lc
                ON lc.LigneId = l.Id
            LEFT JOIN EcareCiments c
                ON c.Id = lc.CimentId
            WHERE (@Usine IS NULL OR z.Usine = @Usine);

            -- 2) Tous les produits
            SELECT
                c.Id,
                c.Name,
                c.[Type]
            FROM EcareCiments c
            ORDER BY c.Name;
            """;

            try
            {
                await uow.BeginAsync(ct);

                var conn = uow.Connection
                           ?? throw new InvalidOperationException("UnitOfWork.Connection is null in GetCementLineMatrixHandler.");

                using var multi = await conn.QueryMultipleAsync(
                    new CommandDefinition(
                        sql,
                        new { request.Usine },
                        transaction: uow.Transaction,
                        cancellationToken: ct));

                var lineRows = (await multi.ReadAsync<LineRow>()).ToList();
                var products = (await multi.ReadAsync<CementVm>()).ToList();

                var lines = lineRows
                    .GroupBy(r => new
                    {
                        r.LigneId,
                        Name = r.LineName ?? string.Empty,
                        Status = r.Status ?? 0,
                        Capacity = r.Capacity ?? 0,
                        Usine = r.Usine ?? string.Empty,
                        TypeActivite = r.TypeActivite ?? string.Empty,
                        TypeOperation = r.TypeOperation ?? string.Empty
                    })
                    .Select(g =>
                    {
                        var prods = g
                            .Where(r => r.CimentId.HasValue)
                            .Select(r => new LineProductVm(
                                r.CimentId!.Value,
                                r.CimentName ?? string.Empty,
                                r.CimentType ?? string.Empty,
                                r.ActifProduct ?? false))
                            .OrderBy(p => p.Name)
                            .ToList();

                        return new LineVm(
                            g.Key.LigneId,
                            g.Key.Name,
                            g.Key.Status,
                            g.Key.Capacity,
                            g.Key.Usine,
                            g.Key.TypeActivite,
                            g.Key.TypeOperation,
                            prods);
                    })
                    .OrderBy(l => l.TypeActivite)
                    .ThenBy(l => l.Name)
                    .ToList();

                var matrix = new CementMatrixVm(lines, products);

                await uow.CommitAsync(ct);

                return Result<CementMatrixVm>.Ok(matrix);
            }
            catch (Exception ex)
            {
                log.LogError(ex,
                    "Erreur lors du chargement de la matrice lignes/produits pour l'usine {Usine}",
                    request.Usine);

                await uow.RollbackAsync(ct);
                return Result<CementMatrixVm>.Fail("Erreur lors du chargement de la configuration des lignes.");
            }
        }

        private sealed class LineRow
        {
            public int LigneId { get; init; }
            public string? LineName { get; init; }
            public int? Status { get; init; }
            public int? Capacity { get; init; }
            public string? Usine { get; init; }
            public string? TypeActivite { get; init; }
            public string? TypeOperation { get; init; }

            public int? CimentId { get; init; }
            public string? CimentName { get; init; }
            public string? CimentType { get; init; }

            public bool? ActifProduct { get; init; }   // NEW
        }
    }
}
