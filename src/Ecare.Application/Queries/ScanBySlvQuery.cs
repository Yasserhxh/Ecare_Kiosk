using MediatR;
using Ecare.Application.Dtos;
using Ecare.Domain.Dtos;
using Ecare.Shared;

namespace Ecare.Application.Queries;
public sealed record ScanBySlvQuery(string Slv) : IRequest<Result<SlvDtos.ScanBySlvVm>>;
