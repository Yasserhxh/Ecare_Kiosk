using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Domain.ValueObjects
{
    public sealed record CementMatrixRowVm(
    int CimentId,
    string Product,  // "Id - Name"
    string Type,     // SAC / VRAC / PAL / ...
    IReadOnlyList<CementLineVm> Lines);
}
