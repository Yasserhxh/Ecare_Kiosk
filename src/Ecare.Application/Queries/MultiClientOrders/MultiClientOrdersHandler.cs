using Dapper;
using Ecare.Domain.ValueObjects;
using Ecare.Shared;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.MultiClientOrders
{
    public sealed class MultiClientOrdersHandler
    : IRequestHandler<MultiClientOrdersQuery, Result<MultiClientOrdersVm>>
    {
        private readonly IConfiguration _cfg;
        private readonly ILogger<MultiClientOrdersHandler> _log;

        public MultiClientOrdersHandler(IConfiguration cfg, ILogger<MultiClientOrdersHandler> log)
        {
            _cfg = cfg;
            _log = log;
        }

        public async Task<Result<MultiClientOrdersVm>> Handle(MultiClientOrdersQuery request, CancellationToken ct)
        {
            try
            {
                string connStr = _cfg.GetConnectionString("SqlServer")!;
                using var conn = new SqlConnection(connStr);

                var rows = (await conn.QueryAsync<ParkingScanRow>(
                    "sp_GetParkingScanData",
                    new { RfidCard = request.Slv },
                    commandType: CommandType.StoredProcedure
                )).AsList();

                if (rows.Count == 0)
                    return Result<MultiClientOrdersVm>.Fail("NO_DATA");

                return Result<MultiClientOrdersVm>.Ok(BuildResponse(request.Slv, rows));
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "MultiClientOrders SP failed");
                return Result<MultiClientOrdersVm>.Fail(ex.Message);
            }
        }

        private MultiClientOrdersVm BuildResponse(string slv, List<ParkingScanRow> rows)
        {
            var vm = new MultiClientOrdersVm { Slv = slv };

            var clientGroups = rows
                .GroupBy(r => new { r.ClientId, r.ClientCode, r.ClientName });

            foreach (var cg in clientGroups)
            {
                var client = new ClientNode
                {
                    ClientId = cg.Key.ClientId ?? 0,
                    ClientCode = cg.Key.ClientCode,
                    ClientName = cg.Key.ClientName,
                    Chantiers = new()
                };

                var chantierGroups = cg
                    .GroupBy(r => new { r.ChantierId, r.ChantierCode, r.ChantierName });

                foreach (var chg in chantierGroups)
                {
                    var chantier = new ChantierNode
                    {
                        ChantierId = chg.Key.ChantierId ?? 0,
                        ChantierCode = chg.Key.ChantierCode,
                        ChantierName = chg.Key.ChantierName
                    };

                    var orderGroups = chg.GroupBy(r => r.OrderId);

                    foreach (var og in orderGroups)
                    {
                        if (og.Key is null) continue;

                        var first = og.First();
                        chantier.Order = new OrderNode
                        {
                            OrderId = first.OrderId ?? 0,
                            Number = first.OrderNumber,
                            Destination = first.Destination,
                            DeliveryMode = first.DeliveryMode,
                            TruckPlate = first.OrderTruckPlate,
                            Status = first.OrderStatus,
                            Items = og
                                .Where(r => r.ProductId != null)
                                .Select(r => new OrderItemNode
                                {
                                    ProductId = r.ProductId ?? 0,
                                    ProductName = r.ProductName,
                                    Quantity = r.Quantity ?? 0,
                                    Unite = r.Unite,
                                    ImageUrl = r.ImageUrl
                                })
                                .ToList()
                        };
                    }

                    client.Chantiers.Add(chantier);
                }

                vm.Clients.Add(client);
            }

            return vm;
        }
    }

}
