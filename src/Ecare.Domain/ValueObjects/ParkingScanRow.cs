using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Domain.ValueObjects
{
    public sealed class ParkingScanRow
    {
        public int TruckId { get; set; }
        public string TruckPlate { get; set; }
        public string RfidCard { get; set; }

        public int? ClientId { get; set; }
        public string ClientCode { get; set; }
        public string ClientName { get; set; }

        public int? ChantierId { get; set; }
        public string ChantierCode { get; set; }
        public string ChantierName { get; set; }

        public int? OrderId { get; set; }
        public string OrderNumber { get; set; }
        public string Destination { get; set; }
        public string DeliveryMode { get; set; }
        public string OrderTruckPlate { get; set; }
        public string OrderStatus { get; set; }

        public int? ProductId { get; set; }
        public string ProductName { get; set; }
        public int? Quantity { get; set; }
        public string Unite { get; set; }
        public string ImageUrl { get; set; }
    }

}
