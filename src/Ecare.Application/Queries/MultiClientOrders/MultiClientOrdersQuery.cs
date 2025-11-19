using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.MultiClientOrders
{


    namespace Ecare.Application.Queries.MultiClientOrders
    {
        public sealed record MultiClientOrdersQuery(string Slv)
            : IRequest<Result<MultiClientOrdersVm>>;

        public sealed class MultiClientOrdersVm
        {
            public string Slv { get; set; } = string.Empty;
            public string TypeCamion { get; set; } = "";
            public DriverVm Driver { get; set; } = new();
            public List<ClientNode> Clients { get; set; } = new();
        }

        public sealed class DriverVm
        {
            public int DriverId { get; set; }
            public string Nom { get; set; } = string.Empty;
            public string Prenom { get; set; } = string.Empty;
            public string FullName => $"{Nom} {Prenom}";
            public string Plate { get; set; } = string.Empty;
        }

        public sealed class ClientNode
        {
            public int ClientId { get; set; }
            public string ClientCode { get; set; } = string.Empty;
            public string ClientName { get; set; } = string.Empty;
            public List<ChantierNode> Chantiers { get; set; } = new();
        }

        public sealed class ChantierNode
        {
            public int ChantierId { get; set; }
            public string ChantierCode { get; set; } = string.Empty;
            public string ChantierName { get; set; } = string.Empty;
            public OrderNode? Order { get; set; }
        }

        public sealed class OrderNode
        {
            public int OrderId { get; set; }
            public string Number { get; set; } = string.Empty;
            public string Destination { get; set; } = string.Empty;
            public string DeliveryMode { get; set; } = string.Empty;
            public string TruckPlate { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;

            public bool IsInQueue { get; set; }      // NEW
            public List<OrderItemNode> Items { get; set; } = new();
        }

        public sealed class OrderItemNode
        {
            public int ProductId { get; set; }
            public string ProductName { get; set; } = string.Empty;
            public int Quantity { get; set; }
            public string Unite { get; set; } = string.Empty;
            public string? ImageUrl { get; set; }
        }

    }


}
