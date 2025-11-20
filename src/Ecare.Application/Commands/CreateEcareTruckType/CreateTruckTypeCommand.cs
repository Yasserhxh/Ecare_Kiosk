using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.CreateEcareTruckType
{
    public sealed record CreateTruckTypeCommand(string Type)
    : IRequest<Result<int>>;
}
