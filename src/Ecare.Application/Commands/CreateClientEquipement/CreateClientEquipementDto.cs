using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.CreateClientEquipement
{
    public sealed class CreateClientEquipementDto
    {
        public string ClientName { get; set; } = default!;
        public string CarteSLV { get; set; } = default!;
        public string Matricule { get; set; } = default!;
        public string ChauffeurName { get; set; } = default!;
        public string RfidHex { get; set; } = default!;
        public string CodeClientSAP { get; set; } = default!;
        public int PlombsNumber { get; set; }
        public decimal PTAC { get; set; }
        public decimal TARE { get; set; }
        public string CodeTransporteurSap { get; set; } = default!;
        public string TransporteurName { get; set; } = default!;
        public string CodeTruckSap { get; set; } = default!;
        public string CodeTransporteurSapCimar { get; set; } = default!;
        public string PermisConducteur { get; set; } = default!;
        public int IsClient { get; set; }
        public int IsTransporteur { get; set; }
        public int IsDriver { get; set; }
        public string? TruckType { get; set; }
    }

}
