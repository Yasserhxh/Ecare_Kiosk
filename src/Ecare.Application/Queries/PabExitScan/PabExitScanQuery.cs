using MediatR;
using Ecare.Shared;

namespace Ecare.Application.Queries.PabExitScan
{
    public sealed record PabExitScanQuery(string RfidCard)
        : IRequest<Result<PabExitScanVm>>;
}
