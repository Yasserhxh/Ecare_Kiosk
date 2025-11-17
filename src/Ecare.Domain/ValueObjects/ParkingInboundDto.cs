using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Domain.ValueObjects
{
    public sealed class ParkingInboundDto
    {
        public string? carteSlv { get; set; }
        public string? slv { get; set; }
        public string? deviceId { get; set; }
      
    }
}
