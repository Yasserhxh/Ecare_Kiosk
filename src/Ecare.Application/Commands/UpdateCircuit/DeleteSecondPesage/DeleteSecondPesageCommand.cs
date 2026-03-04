using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.UpdateCircuit.DeleteSecondPesage
{
    public class DeleteSecondPesageCommand : IRequest<bool>
    {
        public int Id { get; set; }
        public required string SecondPesageCanceledBy { get; set; }
    }
}
