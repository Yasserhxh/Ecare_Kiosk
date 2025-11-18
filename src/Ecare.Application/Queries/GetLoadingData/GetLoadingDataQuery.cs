using Ecare.Application.Dtos;
using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.GetLoadingData
{
    public sealed record GetLoadingDataQuery(string Rfid)
    : IRequest<Result<GetLoadingDataVm>>;

    public sealed record GetLoadingDataVm(
    int DriverId,
    string DriverName,
    string Plate,
    string CarteSLV,
    string? ClientName,
    bool? SapOk,
    decimal? FirstWeight,
    DateTime? PabEntryAt,
    OrderDto? Order
);
}
