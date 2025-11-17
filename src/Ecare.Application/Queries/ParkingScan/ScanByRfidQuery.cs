using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Ecare.Domain.ValueObjects.ParkingScanDtos;

namespace Ecare.Application.Queries.ParkingScan
{
    public sealed record ScanByRfidQuery(string Rfid) : IRequest<ScanResult>;

}
