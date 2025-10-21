using Ecare.Domain.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Domain.Interfaces
{
    public interface IScanResultBroadcaster
    {
        Task BroadcastAsync(ScanResultMessage message, CancellationToken ct = default);
    }
}
