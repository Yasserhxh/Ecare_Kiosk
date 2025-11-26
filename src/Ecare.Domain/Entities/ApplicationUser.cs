using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Domain.Entities
{
    public class ApplicationUser : IdentityUser<string>
    {
        public string? Prenom { get; set; }
        public string? Nom { get; set; }
        public bool IsActive { get; set; } = true;

        public string? RaisonSociale { get; set; } = "ASMENT-TEMARA";
    }

    public class ApplicationRole : IdentityRole<string> { }

}
