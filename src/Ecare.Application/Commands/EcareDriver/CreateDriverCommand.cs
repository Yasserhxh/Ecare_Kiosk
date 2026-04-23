using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.EcareDriver
{
public sealed record CreateDriverCommand(
    string Cin,
    string Nom,
    string Prenom,
    string Numero,
    string? Permis = null,
    string? NomComplet = null
) : IRequest<Result<int>>;
}
