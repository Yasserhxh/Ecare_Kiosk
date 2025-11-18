using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.MultiClientOrders
{
    public sealed record MultiClientOrdersQuery(string Slv) : IRequest<Result<MultiClientOrdersVm>>;

    public sealed class MultiClientOrdersVm
    {
        public string Slv { get; set; }
        public List<ClientNode> Clients { get; set; } = new();
    }

    public sealed class ClientNode
    {
        public int ClientId { get; set; }
        public string ClientCode { get; set; }
        public string ClientName { get; set; }

        public List<ChantierNode> Chantiers { get; set; } = new();
    }

    public sealed class ChantierNode
    {
        public int ChantierId { get; set; }
        public string ChantierCode { get; set; }
        public string ChantierName { get; set; }

        public OrderNode? Order { get; set; }
    }

    public sealed class OrderNode
    {
        public int OrderId { get; set; }
        public string Number { get; set; }
        public string Destination { get; set; }
        public string DeliveryMode { get; set; }
        public string TruckPlate { get; set; }
        public string Status { get; set; }

        public List<OrderItemNode> Items { get; set; } = new();
    }

    public sealed class OrderItemNode
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public string Unite { get; set; }
        public string ImageUrl { get; set; }
    }

}
