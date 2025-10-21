using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Ecare.Infrastructure.Services.AzureSignalR
{
    public sealed class RfidEnvelope
    {
        // Your console sends: new { carteSlv, tsUtc = DateTime.UtcNow }
        [JsonPropertyName("carteSlv")] public string? CarteSlv { get; init; }
        [JsonPropertyName("tsUtc")] public DateTime? TsUtc { get; init; }
    }
}
