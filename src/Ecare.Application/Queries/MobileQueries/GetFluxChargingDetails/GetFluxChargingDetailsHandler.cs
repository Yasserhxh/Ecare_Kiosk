using Dapper;
using Ecare.Shared;
using MediatR;

namespace Ecare.Application.Queries.MobileQueries.GetFluxChargingDetails
{
    public sealed class GetFluxChargingDetailsHandler
        : IRequestHandler<GetFluxChargingDetailsQuerie, Result<IReadOnlyList<FluxStageSummary>>>
    {
        private readonly IUnitOfWork _uow;
        public GetFluxChargingDetailsHandler(IUnitOfWork uow) => _uow = uow;

        private double GetDuration(DateTime? start, DateTime? end)
        {
            if (!start.HasValue) return 0;
            if (!end.HasValue) return (DateTime.Now - start.Value).TotalMinutes;
            return (end.Value - start.Value).TotalMinutes;
        }

        public async Task<Result<IReadOnlyList<FluxStageSummary>>> Handle(GetFluxChargingDetailsQuerie request, CancellationToken ct)
        {
            const string sql = @"
            SELECT 
                e.Id,
                e.BonDeCommande,
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
            LEFT JOIN dbo.Orders o       ON e.OrderId = o.Id
            LEFT JOIN dbo.Ecare_OrderItems oi ON o.Id = oi.OrderId
            LEFT JOIN dbo.EcareCiments ec     ON ec.Id = oi.ProductId
            WHERE e.ParkedAt IS NOT NULL
              AND (@Type IS NULL OR ec.Type = @Type)
              AND (@DateFrom IS NULL OR CONVERT(date, e.ParkedAt) >= CONVERT(date, @DateFrom))
              AND (@DateTo   IS NULL OR CONVERT(date, e.ParkedAt) <= CONVERT(date, @DateTo));
";

            try
            {
                await _uow.BeginAsync(ct);

                var rows = (await _uow.Connection.QueryAsync(
                    sql,
                    new
                    {
                        request.Type,
                        request.DateFrom,
                        request.DateTo
                    },
                    _uow.Transaction))
                    .ToList();

                await _uow.CommitAsync(ct);

                // 1) Map each row to a stage + duration
                var rawItems = rows.Select(r =>
                {
                    DateTime? parked = r.ParkedAt;
                    DateTime? entry = r.PabEntryAt;
                    DateTime? start = r.StartChargingAt;
                    DateTime? finish = r.FinishedChargingAt;
                    DateTime? exit = r.PabExitAt;

                    string stage;
                    double minutes;

                    // PARC: ParkedAt -> PabEntryAt (if no entry yet, until now)
                    if (parked != null && entry == null)
                    {
                        stage = "PARC";
                        minutes = GetDuration(parked, entry);
                    }
                    // USINE: PabEntryAt -> StartChargingAt
                    else if (entry != null && start == null)
                    {
                        stage = "USINE";
                        minutes = GetDuration(entry, start);
                    }
                    // CHARGEMENT: StartChargingAt -> FinishedChargingAt
                    else if (start != null && finish == null)
                    {
                        stage = "CHARGEMENT";
                        minutes = GetDuration(start, finish);
                    }
                    // SORTIE: FinishedChargingAt -> PabExitAt
                    else if (finish != null)
                    {
                        stage = "SORTIE";
                        minutes = GetDuration(finish, exit);
                    }
                    else
                    {
                        stage = "PARC";
                        minutes = 0;
                    }

                    return new
                    {
                        Stage = stage,
                        Matricule = (string)r.Matricule,
                        ClientName = (string?)r.ClientName,
                        Ligne = (string?)r.Ligne,
                        Type = (string?)r.Type,
                        BonDeCommande = (string?)r.BonDeCommande,
                        MinutesInStage = minutes
                    };
                }).ToList();

                // 2) Deduplicate: same Stage + Matricule + BonDeCommande + Type = ONE truck
                var items = rawItems
                    .GroupBy(x => new { x.Stage, x.Matricule, x.BonDeCommande, x.Type })
                    .Select(g =>
                    {
                        var any = g.First();
                        var maxMinutes = g.Max(x => x.MinutesInStage); // keep max duration for that group

                        return new
                        {
                            any.Stage,
                            any.Matricule,
                            any.ClientName,
                            any.Ligne,
                            any.Type,
                            MinutesInStage = maxMinutes
                        };
                    })
                    .ToList();

                // 3) Aggregate by stage, but always return 4 stages
                var stageNames = new[] { "PARC", "USINE", "CHARGEMENT", "SORTIE" };

                var result = stageNames.Select(stage =>
                {
                    var g = items.Where(x => x.Stage == stage).ToList();

                    if (g.Count == 0)
                    {
                        return new FluxStageSummary(
                            Stage: stage,
                            TruckCount: 0,
                            MinMinutes: 0,
                            MaxMinutes: 0,
                            AvgMinutes: 0,
                            Trucks: new List<FluxStageItem>()
                        );
                    }

                    var trucks = g.Select(x => new FluxStageItem(
                        x.Matricule,
                        x.ClientName,
                        x.Ligne,
                        x.Type,
                        x.MinutesInStage
                    )).ToList();

                    return new FluxStageSummary(
                        Stage: stage,
                        TruckCount: trucks.Count,
                        MinMinutes: trucks.Min(t => t.MinutesInStage),
                        MaxMinutes: trucks.Max(t => t.MinutesInStage),
                        AvgMinutes: trucks.Average(t => t.MinutesInStage),
                        Trucks: trucks
                    );
                }).ToList();

                return Result<IReadOnlyList<FluxStageSummary>>.Ok(result);
            }
            catch (Exception ex)
            {
                await _uow.RollbackAsync(ct);
                return Result<IReadOnlyList<FluxStageSummary>>.Fail($"Query failed: {ex.Message}");
            }
        }
    }
}
