using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.EcareDriver
{
    public sealed class EcareDriver
    {
        public int Id { get; set; }
        public string? Cin { get; set; }
        public string? Nom { get; set; }
        public string? Prenom { get; set; }
        public string? Numero { get; set; }
    }

}
