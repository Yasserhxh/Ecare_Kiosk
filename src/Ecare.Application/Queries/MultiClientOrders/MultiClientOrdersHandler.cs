using Dapper;
using Ecare.Application.Queries.MultiClientOrders.Ecare.Application.Queries.MultiClientOrders;
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

        public async Task<Result<MultiClientOrdersVm>> Handle(
            MultiClientOrdersQuery request,
            CancellationToken ct)
        {
            var connStr = _cfg.GetConnectionString("SqlServer");
            using var conn = new SqlConnection(connStr);

            var rows = await conn.QueryAsync<dynamic>(
                "sp_GetParkingScanData",
                new { RfidCard = request.Slv },
                commandType: CommandType.StoredProcedure);

            if (!rows.Any())
                return Result<MultiClientOrdersVm>.Fail("NO_DATA");

            var vm = new MultiClientOrdersVm { Slv = request.Slv };

            // extract driver
            var row0 = rows.First();
            vm.Driver = new DriverVm
            {
                DriverId = row0.DriverId,
                Nom = row0.DriverNom,
                Prenom = row0.DriverPrenom,
                Plate = row0.TruckPlate,
            };

            // Build client/chantier/order tree
            var clientMap = new Dictionary<int, ClientNode>();

            foreach (var r in rows)
            {
                if (r.ClientId == null)
                    continue;

                if (!clientMap.TryGetValue((int)r.ClientId, out var client))
                {
                    client = new ClientNode
                    {
                        ClientId = r.ClientId,
                        ClientCode = r.ClientCode,
                        ClientName = r.ClientName
                    };
                    clientMap[r.ClientId] = client;
                }

                // chantier
                if (r.ChantierId != null)
                {
                    var chantier = client.Chantiers.FirstOrDefault(x => x.ChantierId == r.ChantierId);
                    if (chantier == null)
                    {
                        chantier = new ChantierNode
                        {
                            ChantierId = r.ChantierId,
                            ChantierCode = r.ChantierCode,
                            ChantierName = r.ChantierName
                        };
                        client.Chantiers.Add(chantier);
                    }

                    // order
                    if (r.OrderId != null && chantier.Order == null)
                    {
                        chantier.Order = new OrderNode
                        {
                            OrderId = r.OrderId,
                            Number = r.OrderNumber,
                            Destination = r.Destination,
                            DeliveryMode = r.DeliveryMode,
                            TruckPlate = r.OrderTruckPlate,
                            Status = r.OrderStatus,
                            Items = new List<OrderItemNode>()
                        };
                    }

                    // items
                    if (r.OrderId != null && r.ProductId != null)
                    {
                        chantier.Order.Items.Add(new OrderItemNode
                        {
                            ProductId = r.ProductId,
                            ProductName = r.ProductName,
                            Quantity = r.Quantity,
                            Unite = r.Unite,
                            ImageUrl = r.ImageUrl
                        });
                    }
                }
            }

            vm.Clients = clientMap.Values.ToList();
            return Result<MultiClientOrdersVm>.Ok(vm);
        }
    }


}
