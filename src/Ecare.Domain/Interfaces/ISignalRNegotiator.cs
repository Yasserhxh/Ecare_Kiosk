using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ecare.Domain.ValueObjects;

namespace Ecare.Domain.Interfaces
{
    public interface ISignalRNegotiator
    {
        /// <summary>
        /// Returns a connect URL and short-lived access token for the given hub and optional userId.
        /// </summary>
        Task<NegotiateResult> NegotiateAsync(NegotiateRequest request, CancellationToken ct = default);
    }
}
