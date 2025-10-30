using Ecare.Shared;
using MediatR;

namespace Ecare.Application.Commands.Queue.PinTruck
{
    public sealed record TogglePinByMatriculeCommand(string Matricule)
    : IRequest<Result<int>>;
}
