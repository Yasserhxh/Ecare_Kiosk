using MediatR;
using Ecare.Application.Dtos;
using Ecare.Shared;

namespace Ecare.Application.Queries;
public sealed record GetCimentsQuery() : IRequest<Result<IEnumerable<CimentDto>>>;


