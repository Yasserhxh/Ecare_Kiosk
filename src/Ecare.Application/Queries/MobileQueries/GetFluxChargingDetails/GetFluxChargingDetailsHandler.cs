using Dapper;
using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.MobileQueries.GetFluxChargingDetails
{
    public sealed class GetFluxChargingDetailsByIdHandler
    : IRequestHandler<GetFluxChargingDetailsByIdQuery, Result<FluxChargingGroupVm?>>
    {
        private readonly IUnitOfWork _uow;
        public GetFluxChargingDetailsByIdHandler(IUnitOfWork uow) => _uow = uow;

        public async Task<Result<FluxChargingGroupVm?>> Handle(GetFluxChargingDetailsByIdQuery request, CancellationToken ct)
        {
            const string sql = @"
SELECT
    ef.CarteSlv,
    CAST(ef.FirstWeight AS DECIMAL(18,3)) AS FirstWeight,
    ef.Matricule,
    ef.ClientName,
    ec.Name   AS ProductName,
    CAST(oi.Quantity AS DECIMAL(18,3)) AS Quantity,
    ec.Type   AS ProductType,
    ef.StartChargingAt,
    ef.FinishedChargingAt,
    ef.Ligne
FROM dbo.EcareFlux ef
JOIN dbo.Orders o            ON ef.OrderId = o.Id
JOIN dbo.Ecare_OrderItems oi ON o.Id = oi.OrderId
JOIN dbo.EcareCiments ec     ON ec.Id = oi.ProductId
WHERE ef.Id = @EfId
ORDER BY ec.Name;";

            try
            {
                await _uow.BeginAsync(ct);

                var rows = (await _uow.Connection.QueryAsync<FluxChargingRow>(
                    new CommandDefinition(sql, new { EfId = request.EfId }, _uow.Transaction, cancellationToken: ct)))
                    .ToList();

                await _uow.CommitAsync(ct);

                if (rows.Count == 0)
                    return Result<FluxChargingGroupVm?>.Ok(null); // let the endpoint turn this into 404

                var first = rows[0];
                var isTcharging = rows.Any(x => x.StartChargingAt.HasValue);
                var isFinished = rows.Any(x => x.FinishedChargingAt.HasValue);

                var items = rows.Select(x => new FluxChargingItemDto(x.ProductName, x.Quantity, x.ProductType)).ToList();

                var vm = new FluxChargingGroupVm(
                    matricule: first.Matricule,
                    carteSlv: first.CarteSlv,
                    clientName: first.ClientName,
                    firstWeight: first.FirstWeight,
                    ligne: first.Ligne,
                    isTcharging: isTcharging,
                    isFinished: isFinished,
                    startChargingAt: rows.Select(x => x.StartChargingAt).FirstOrDefault(x => x.HasValue),
                    finishedChargingAt: rows.Select(x => x.FinishedChargingAt).FirstOrDefault(x => x.HasValue),
                    items: items
                );

                return Result<FluxChargingGroupVm?>.Ok(vm);
            }
            catch (Exception ex)
            {
                await _uow.RollbackAsync(ct);
                return Result<FluxChargingGroupVm?>.Fail($"Query failed: {ex.Message}");
            }
        }
    }
}
