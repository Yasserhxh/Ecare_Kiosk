using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.GetPartner
{
    public sealed class PartnerVm
    {
        public int Id { get; set; }
        public string Code { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string PartnerType { get; set; } = default!;
        public DateTime DateCreation { get; set; }
        public bool Actif { get; set; }
    }

}
