using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.ChantierDropdowByIdClient
{
    public sealed class ChantierDropdownVm
    {
        public int Id { get; set; }
        public string DisplayName { get; set; } = default!;
    }

}
