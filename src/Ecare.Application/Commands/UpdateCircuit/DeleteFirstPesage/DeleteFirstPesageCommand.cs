using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.UpdateCircuit.DeleteFirstPesage
{
    public class DeleteFirstPesageCommand :IRequest<bool>
    {
        public int Id { get; set; }
        public required string FirstPesageCanceledBy { get; set; }
    }
}
