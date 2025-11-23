using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Domain.ValueObjects
{
    public sealed record CementLineFlatVm(
       string ZoneName,         // z.Usine
       string? OperationType,   // z.TypeOperation
       int LigneId,             // l.Id
       string LineName,         // l.Nom
       int LineStatus,          // l.Status
       int LineCapacity,        // l.Capacity
       string? Products         // STRING_AGG(c.Name)
   );
}
