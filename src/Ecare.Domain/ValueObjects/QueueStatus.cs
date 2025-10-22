using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Domain.ValueObjects
{
    public enum QueueStatus
    {
        EnValidation = 0,
        EncourTraitement = 1,
        PadEntree = 2,
        EnChargement = 3,
        PabSortie =4
    }
}
