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
        : IRequestHandler<GetCementLineMatrixQuery, Result<IReadOnlyList<CementMatrixRowVm>>>
    {
        public async Task<Result<IReadOnlyList<CementMatrixRowVm>>> Handle(
            GetCementLineMatrixQuery request,
            CancellationToken ct)
        {
            const string sql = """
            SELECT
                c.Id                                           AS CimentId,
                CAST(c.Id AS nvarchar(10)) + ' - ' + c.Name    AS Product,
                c.Type                                         AS Type,
                l.Id                                           AS LigneId,
                l.Nom                                          AS LineName,
                z.Usine                                        AS Usine,
                z.TypeActivite                                 AS TypeActivite,
                l.Status                                       AS Status
            FROM EcareCiments c
            LEFT JOIN Ecare_LigneCiments lc
                ON lc.CimentId = c.Id
            LEFT JOIN Ecare_Ligne l
                ON lc.LigneId = l.Id
            LEFT JOIN Ecare_Zone_Chargement z
                ON l.ZoneChargementId = z.Id
               AND (@Usine IS NULL OR z.Usine = @Usine)
            ORDER BY c.Name, z.TypeActivite, l.Nom;
            """;

            try
            {
                // Make sure the connection is opened / created
                await uow.BeginAsync(ct); // if your UoW uses this to open connection

                var conn = uow.Connection
                           ?? throw new InvalidOperationException("UnitOfWork.Connection is null in GetCementLineMatrixHandler.");

                var rows = (await conn.QueryAsync<CementLineRow>(
                        new CommandDefinition(
                            sql,
                            new { request.Usine },
                            transaction: uow.Transaction,
                            cancellationToken: ct)))
                    .ToList();

                var grouped = rows
                    .GroupBy(r => new
                    {
                        r.CimentId,
                        Product = r.Product ?? string.Empty,
                        Type = r.Type ?? string.Empty
                    })
                    .Select(g =>
                    {
                        var lines = g
                            .Where(r => r.LigneId.HasValue)
                            .Select(r => new CementLineVm(
                                r.LigneId!.Value,
                                r.LineName ?? string.Empty,
                                r.Usine ?? string.Empty,
                                r.TypeActivite ?? string.Empty,
                                r.Status ?? 0,
                                (r.Status ?? 0) == 1 ? "A" : "S"
                            ))
                            .OrderBy(l => l.TypeActivite)
                            .ThenBy(l => l.LineName)
                            .ToList();

                        return new CementMatrixRowVm(
                            g.Key.CimentId,
                            g.Key.Product,
                            g.Key.Type,
                            lines);
                    })
                    .OrderBy(x => x.Product)
                    .ToList();

                await uow.CommitAsync(ct);

                return Result<IReadOnlyList<CementMatrixRowVm>>.Ok(grouped);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error in GetCementLineMatrix for usine {Usine}", request.Usine);
                await uow.RollbackAsync(ct);
                return Result<IReadOnlyList<CementMatrixRowVm>>.Fail("Erreur lors du chargement de la matrice de lignes ciment.");
            }
        }

        private sealed class CementLineRow
        {
            public int CimentId { get; init; }
            public string? Product { get; init; }
            public string? Type { get; init; }
            public int? LigneId { get; init; }
            public string? LineName { get; init; }
            public string? Usine { get; init; }
            public string? TypeActivite { get; init; }
            public int? Status { get; init; }
        }
    }
}
