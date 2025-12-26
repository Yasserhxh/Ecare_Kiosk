using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.MergeParkingWithSap
{
    public sealed record MergeParkingWithSapCommand(string Matricule, string CodeSapCommande)
    : IRequest<Result<int>>;

}
