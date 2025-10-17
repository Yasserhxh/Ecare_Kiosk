using MediatR;
using Ecare.Application.Dtos;
using Ecare.Shared;

namespace Ecare.Application.Queries;

public sealed record GetFluxQualiteQuery() : IRequest<Result<IEnumerable<FluxQualiteDto>>>;
