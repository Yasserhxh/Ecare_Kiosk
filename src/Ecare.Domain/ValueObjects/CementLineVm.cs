using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Domain.ValueObjects
{
    public sealed record CementLineVm(
    int LigneId,
    string LineName,
    string Usine,
    string TypeActivite,
    int Status,
    string StatusLabel);
}
