using Dapper;
using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.GetFluxStagesSummary
{
    public sealed class GetFluxStagesSummaryHandler
    : IRequestHandler<GetFluxStagesSummaryQuery, Result<IReadOnlyList<FluxStageSummary>>>
    {
        private readonly IUnitOfWork _uow;
        public GetFluxStagesSummaryHandler(IUnitOfWork uow) => _uow = uow;

        public async Task<Result<IReadOnlyList<FluxStageSummary>>> Handle(GetFluxStagesSummaryQuery request, CancellationToken ct)
        {
            const string sql = @"
SELECT 
    e.Id,
    e.Matricule,
    e.ClientName,
    e.Ligne,
    ec.Type,
    e.ParkedAt,
    e.PabEntryAt,
    e.StartChargingAt,
    e.FinishedChargingAt,
    e.PabExitAt
FROM dbo.EcareFlux e
LEFT JOIN dbo.Orders o ON e.OrderId = o.Id
LEFT JOIN dbo.Ecare_OrderItems oi ON o.Id = oi.OrderId
LEFT JOIN dbo.EcareCiments ec ON ec.Id = oi.ProductId
WHERE e.ParkedAt IS NOT NULL
  AND (@Type IS NULL OR ec.Type = @Type);";

            try
            {
                await _uow.BeginAsync(ct);
                var rows = (await _uow.Connection.QueryAsync(sql, new { request.Type }, _uow.Transaction)).ToList();
                await _uow.CommitAsync(ct);

                var now = DateTime.Now;

                var items = rows.Select(r =>
                {
                    string stage;
                    DateTime refTime;

                    var parked = (DateTime?)r.ParkedAt;
                    var entry = (DateTime?)r.PabEntryAt;
                    var start = (DateTime?)r.StartChargingAt;
                    var finish = (DateTime?)r.FinishedChargingAt;

                    if (parked != null && entry == null)
                    {
                        stage = "PARC";
                        refTime = parked.Value;
                    }
                    else if (entry != null && start == null)
                    {
                        stage = "USINE";
                        refTime = entry.Value;
                    }
                    else if (start != null && finish == null)
                    {
                        stage = "CHARGEMENT";
                        refTime = start.Value;
                    }
                    else if (finish != null)
                    {
                        stage = "SORTIE";
                        refTime = finish.Value;
                    }
                    else
                    {
                        stage = "UNKNOWN";
                        refTime = parked ?? now;
                    }

                    var mins = (now - refTime).TotalMinutes;

                    return new
                    {
                        Stage = stage,
                        Matricule = (string)r.Matricule,
                        ClientName = (string?)r.ClientName,
                        Ligne = (string?)r.Ligne,
                        Type = (string?)r.Type,
                        MinutesInStage = mins
                    };
                }).ToList();

                var grouped = items
                    .GroupBy(x => x.Stage)
                    .Select(g =>
                    {
                        var trucks = g.Select(x => new FluxStageItem(
                            x.Matricule, x.ClientName, x.Ligne, x.Type, DateTime.Now.AddMinutes(-x.MinutesInStage), x.MinutesInStage)).ToList();

                        return new FluxStageSummary(
                            Stage: g.Key,
                            TruckCount: trucks.Count,
                            MinMinutes: trucks.Min(x => x.MinutesInStage),
                            MaxMinutes: trucks.Max(x => x.MinutesInStage),
                            AvgMinutes: trucks.Average(x => x.MinutesInStage),
                            Trucks: trucks);
                    })
                    .OrderBy(s => s.Stage)
                    .ToList();

                return Result<IReadOnlyList<FluxStageSummary>>.Ok(grouped);
            }
            catch (Exception ex)
            {
                await _uow.RollbackAsync(ct);
                return Result<IReadOnlyList<FluxStageSummary>>.Fail($"Query failed: {ex.Message}");
            }
        }
    }
}
