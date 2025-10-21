using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Domain.ValueObjects
{
    public sealed class NegotiateResult
    {
        public required string Url { get; init; }
        public required string AccessToken { get; init; }
        public required string Hub { get; init; }
    }
}
