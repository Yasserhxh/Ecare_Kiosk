using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Domain.ValueObjects
{
    public sealed record CementVm(
    int Id,
    string Name,
    string Type);
}
