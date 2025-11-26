using Ecare.Shared;
using MediatR;

namespace Ecare.Application.Queries;

public sealed record PabEntryScanBySlvQuery(string Slv) : IRequest<Result<ScanBySlvVm>>;
