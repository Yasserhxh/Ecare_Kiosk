using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.PartnerTruck
{
    public sealed class PartnerTruckVm
    {
        public string Code { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string Matricule { get; set; } = default!;
        public string TruckType { get; set; } = default!;
        public int PartnerType { get; set; }
        public string TypePartnerName { get; set; } = default!;
    }

    public sealed class PartnerTruckPagedResult
    {
        public int TotalCount { get; set; }
        public IEnumerable<PartnerTruckVm> Items { get; set; } = [];
    }


}
