using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Infrastructure.Storage
{
    public sealed class BlobStorageOptions
    {
        public string ConnectionString { get; set; } = default!;
        public string ContainerName { get; set; } = "ecare-files";
        public int SasExpiryMinutes { get; set; } = 15;
        public int MaxFileSizeMb { get; set; } = 20;
    }

   
}
