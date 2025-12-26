using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.GetTruck
{
    public sealed class TruckDropdownVm
    {
        public int Id { get; set; }
        public string Matricule { get; set; } = default!;
    }

}
