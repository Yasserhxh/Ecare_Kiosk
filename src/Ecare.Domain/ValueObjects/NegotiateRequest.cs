using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Domain.ValueObjects
{
    public sealed class NegotiateRequest
    {
        /// <summary>Hub name; if null/empty, use default from config.</summary>
        public string? HubName { get; init; }
    }
}
