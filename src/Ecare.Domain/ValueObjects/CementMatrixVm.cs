using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Domain.ValueObjects
{
    public sealed record CementMatrixVm(
    IReadOnlyList<LineVm> Lines,
    IReadOnlyList<CementVm> Products);
}
