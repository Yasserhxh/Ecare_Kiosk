using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ecare.Domain.Dtos;


namespace Ecare.Domain.Interfaces
{
    public interface ISlvScanPublisher
    {
        Task PublishAsync(SlvDtos.ScanBySlvVm vm, CancellationToken ct = default);
    }

    

}
