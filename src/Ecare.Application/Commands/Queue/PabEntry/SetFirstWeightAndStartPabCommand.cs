using Ecare.Shared;
using MediatR;

namespace Ecare.Application.Commands.Queue.PabEntry;

public sealed record SetFirstWeightAndStartPabCommand(
    string Matricule,
    int FirstWeight,
    int? OrderId = null
) : IRequest<Result<int>>;
