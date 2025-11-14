using Dapper;
using Ecare.Domain.ValueObjects;
using Ecare.Shared;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries
{
    public sealed class GetChargingDetailsHandler(
        IUnitOfWork uow,
        ILogger<GetChargingDetailsHandler> log)
        : IRequestHandler<GetChargingDetailsQuery, Result<ChargingDetailsVm>>
    {
        public async Task<Result<ChargingDetailsVm>> Handle(
            GetChargingDetailsQuery request,
            CancellationToken ct)
        {
            const string sql = """
            SELECT 
                EF.CarteSlv,
                EF.FirstWeight,
                EF.Matricule,
                EF.ClientName,
                OT.ProductId,
                OT.Quantity,
                EC.[Type],
                EC.Name AS ProductName
            FROM Orders O
            JOIN EcareFlux EF 
                ON O.Id = EF.OrderId
            JOIN Ecare_OrderItems OT 
                ON OT.OrderId = O.Id
            JOIN EcareCiments EC 
                ON EC.Id = OT.ProductId
            WHERE EF.id = @Fluxid;
            """;

            try
            {
                await uow.BeginAsync(ct);

                var conn = uow.Connection
                           ?? throw new InvalidOperationException("UnitOfWork.Connection is null in GetChargingDetailsHandler.");

                var rows = (await conn.QueryAsync<Row>(
                    new CommandDefinition(
                        sql,
                        new { Fluxid = request.fluxid },
                        transaction: uow.Transaction,
                        cancellationToken: ct)))
                    .ToList();

                if (rows.Count == 0)
                {
                    await uow.RollbackAsync(ct);
                    return Result<ChargingDetailsVm>.Fail(
                        "Aucune donnée de chargement trouvée pour cette carte SLV.");
                }

                var first = rows[0];

                var items = rows
                    .Select(r => new ChargingItemVm(
                        r.ProductId,
                        r.Quantity,
                        r.Type ?? string.Empty,
                        r.ProductName ?? string.Empty))
                    .ToList();

                var vm = new ChargingDetailsVm(
                    first.CarteSlv,
                    first.FirstWeight,
                    first.Matricule ?? string.Empty,
                    first.ClientName ?? string.Empty,
                    items);

                await uow.CommitAsync(ct);

                return Result<ChargingDetailsVm>.Ok(vm);
            }
            catch (Exception ex)
            {
                log.LogError(ex,
                    "Erreur lors du chargement des détails de chargement pour la carte SLV {CarteSlv}",
                    request.fluxid);

                await uow.RollbackAsync(ct);
                return Result<ChargingDetailsVm>.Fail(
                    "Erreur technique lors de la récupération des détails de chargement.");
            }
        }

        private sealed class Row
        {
            public int CarteSlv { get; init; }
            public decimal? FirstWeight { get; init; }   // adapte le type si besoin
            public string? Matricule { get; init; }
            public string? ClientName { get; init; }
            public int ProductId { get; init; }
            public decimal Quantity { get; init; }       // idem
            public string? Type { get; init; }
            public string? ProductName { get; init; }
        }
    }
}
