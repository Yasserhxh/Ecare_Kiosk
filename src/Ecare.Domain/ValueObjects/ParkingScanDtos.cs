using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Domain.ValueObjects
{
    public class ParkingScanDtos
    {
        public sealed class ScanResult
        {
            public string Code { get; set; } = string.Empty;
            public TruckDto? Truck { get; set; }
            public List<ClientDto> Clients { get; set; } = new();
            public List<ChantierDto> Chantiers { get; set; } = new();
            public OrderDto? Order { get; set; }
            public List<OrderItemDto> Items { get; set; } = new();
        }

        public sealed class TruckDto
        {
            public int TruckId { get; set; }
            public string Matricule { get; set; } = string.Empty;
            public string? DriverNom { get; set; }
            public string? DriverPrenom { get; set; }
        }

        public sealed class ClientDto
        {
            public int ClientId { get; set; }
            public string ClientCode { get; set; } = string.Empty;
            public string ClientName { get; set; } = string.Empty;
        }

        public sealed class ChantierDto
        {
            public int ChantierId { get; set; }
            public string ChantierCode { get; set; } = string.Empty;
            public string ChantierName { get; set; } = string.Empty;
        }

        public sealed class OrderDto
        {
            public int OrderId { get; set; }
            public string NumeroCommande { get; set; } = string.Empty;
            public DateTime DateCommande { get; set; }
            public string? Destination { get; set; }
            public string? ModeDelivraison { get; set; }
            public string? EmplacementChargement { get; set; }
            public string? MethodeChargement { get; set; }
            public string Statut { get; set; } = string.Empty;
        }

        public sealed class OrderItemDto
        {
            public int ItemId { get; set; }
            public int ProductId { get; set; }
            public string ProductName { get; set; } = string.Empty;
            public string? ProductImage { get; set; }
            public string? ProductDetails { get; set; }
            public string? ProductDescription { get; set; }
            public int Quantity { get; set; }
            public string Unite { get; set; } = string.Empty;
        }

    }
}
