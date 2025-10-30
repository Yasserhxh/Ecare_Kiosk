using Ecare.Application.Dtos;
using Ecare.Shared;
using MediatR;

namespace Ecare.Application.Queries
{
   
    public sealed record GetPabExitDataQuery(string Slv) : IRequest<Result<ScanBySlvPabExit>>;

    public sealed record ScanBySlvPabExit(int FirstWeight,int DriverId, string Plate, string CarteSLV, string? ClientName, bool? SapOk, OrderDto? Order);
}
