using Ecare.Shared;
using MediatR;

namespace Ecare.Application.Commands.ChangeLigneRealtimeCapacity
{
    public sealed record ChangeLigneRealtimeCapacityCommand(int LigneId, int RealtimeCapacity)
        : IRequest<Result<int>>;
}
