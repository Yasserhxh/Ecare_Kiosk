using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Domain.ValueObjects
{
    public sealed record LineVm(
    int LigneId,
    string Name,
    int Status,
    int Capacity,
    string Usine,
    string TypeActivite,
    string TypeOperation,
    IReadOnlyList<LineProductVm> Products);
}
