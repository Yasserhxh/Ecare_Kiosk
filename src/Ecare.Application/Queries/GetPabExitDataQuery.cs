using Ecare.Application.Dtos;
using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries
{
   
    public sealed record GetPabExitDataQuery(string Slv) : IRequest<Result<ScanBySlvPabExit>>;

    public sealed record ScanBySlvPabExit(int FirstWeight,int DriverId, string Plate, string CarteSLV, string? ClientName, bool? SapOk, OrderDto? Order);
}
