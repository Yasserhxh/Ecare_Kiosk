using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.CreateEcareTruck
{
    public sealed class EcareTruckFullVm
    {
        public int Id { get; set; }
        public string Matricule { get; set; } = default!;
        public int PTAC { get; set; }
        public int TARE { get; set; }
        public string? RfidCard { get; set; }
        public int? TruckTypeId { get; set; }
        public string? TruckType { get; set; }
        public int? DriverId { get; set; }
        public string? DriverName { get; set; }
        public int NumberOfSeals { get; set; }
        public DateTime DateCreation { get; set; }
        public bool Actif { get; set; }
    }

}
