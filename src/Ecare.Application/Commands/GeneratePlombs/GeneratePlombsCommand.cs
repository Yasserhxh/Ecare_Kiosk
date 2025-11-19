using Ecare.Shared;
using MediatR;

namespace Ecare.Application.Commands.GeneratePlombs
{
    public sealed record GeneratePlombsCommand(string Matricule)
        : IRequest<Result<GeneratedPlombsVm>>;
}
