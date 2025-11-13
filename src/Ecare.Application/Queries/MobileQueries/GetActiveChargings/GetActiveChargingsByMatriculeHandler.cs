using Dapper;
using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.MobileQueries.GetActiveChargings
{
    public sealed class GetActiveChargingsByMatriculeHandler
    : IRequestHandler<GetActiveChargingsByMatriculeQuery, Result<IReadOnlyDictionary<string, ChargingGroupVm>>>
    {
        private readonly IUnitOfWork _uow;
        public GetActiveChargingsByMatriculeHandler(IUnitOfWork uow) => _uow = uow;

        public async Task<Result<IReadOnlyDictionary<string, ChargingGroupVm>>> Handle(GetActiveChargingsByMatriculeQuery request, CancellationToken ct)
        {
            var sql = @"
                SELECT 
                    e.Id,
                    e.StartChargingAt,
                    e.Matricule,
                    ec.Name,
                    e.BonDeCommande,
                    e.Ligne,
                    CAST(ot.Quantity AS DECIMAL(18,3)) AS Quantity,
                    ot.Unite,
                    ec.Type
                FROM dbo.EcareFlux            AS e
                JOIN dbo.Orders               AS o  ON e.OrderId = o.Id
                JOIN dbo.Ecare_OrderItems     AS ot ON o.Id      = ot.OrderId
                JOIN dbo.EcareCiments         AS ec ON ec.Id     = ot.ProductId
                WHERE e.FinishedChargingAt IS NULL
                  AND e.FirstWeight IS NOT NULL
                ";

                            // Optional filters
                            if (!string.IsNullOrWhiteSpace(request.Ligne))
                                sql += "  AND e.Ligne = @Ligne";
                            if (!string.IsNullOrWhiteSpace(request.Type))
                                sql += "  AND ec.Type = @Type";

                            sql += @"
                ORDER BY e.Matricule, e.Id;";

            try
            {
                await _uow.BeginAsync(ct);

                var rows = (await _uow.Connection.QueryAsync<FluxItemRow>(
                    new CommandDefinition(sql, new { request.Ligne, request.Type }, _uow.Transaction, cancellationToken: ct)))
                    .ToList();

                var grouped = rows
                    .GroupBy(r => r.Matricule)
                    .ToDictionary(
                        g => g.Key,
                        g =>
                        {
                            var firstNonNullStart = g.Select(r => r.StartChargingAt).FirstOrDefault(d => d.HasValue);
                            var anyCharging = g.Any(r => r.StartChargingAt.HasValue);
                            var ligne = g.Select(r => r.Ligne).FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));
                            var bonDeCoammande = g.Select(r => r.BonDeCommande).FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));
                            var items = g.Select(r => new ChargingItemDto(r.Name, r.Quantity, r.Unite, r.Type)).ToList();
                            var fluxIds = g.Select(r => r.Id).Distinct().ToList();

                            return new ChargingGroupVm(
                                matricule: g.Key,
                                isTcharging: anyCharging,
                                startChargingAt: firstNonNullStart,
                                ligne: ligne,
                                bonDeCoammande: bonDeCoammande,
                                fluxIds: fluxIds,
                                items: items);
                        });

                await _uow.CommitAsync(ct);
                return Result<IReadOnlyDictionary<string, ChargingGroupVm>>.Ok(grouped);
            }
            catch (Exception ex)
            {
                await _uow.RollbackAsync(ct);
                return Result<IReadOnlyDictionary<string, ChargingGroupVm>>.Fail($"Query failed: {ex.Message}");
            }
        }
    }
}
