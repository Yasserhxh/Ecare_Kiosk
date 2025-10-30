using Ecare.Application.Dtos;
using Ecare.Shared;
using MediatR;

namespace Ecare.Application.Queries.Loading.GetOrderDetails
{

    public sealed record GetOrderDetailsQuery(string Slv) : IRequest<Result<ScanBySlvLoading>>;

    public sealed record ScanBySlvLoading(int FirstWeight, int DriverId, string Plate, string CarteSLV, string? ClientName, bool? SapOk, OrderDto? Order);
}
