using MediatR;
using Ecare.Application.Dtos;
using Ecare.Shared;

namespace Ecare.Application.Queries;
public sealed record ScanBySlvQuery(string Slv) : IRequest<Result<ScanBySlvVm>>;
public sealed record ScanBySlvVm(int DriverId, string Plate, string CarteSLV, string? ClientName, bool? SapOk, OrderDto? Order);
