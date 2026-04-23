using Ecare.Application.Commands.EcareDriver;
using Ecare.Shared;
using MediatR;

namespace Ecare.Application.Queries.GetDrivers;

public sealed record GetDriverByIdQuery(int Id) : IRequest<Result<EcareDriver>>;
