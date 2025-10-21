using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Infrastructure.Services.AzureSignalR
{
    public sealed class SignalRInfraOptions
    {
        public string? ConnectionString { get; set; }
        public string DefaultHub { get; set; } = "slv_hub";
    }
}
