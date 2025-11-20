using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.GetDrivers
{
    public sealed class DriverDropdownVm
    {
        public int Id { get; set; }
        public string FullName { get; set; } = default!;
    }

}
