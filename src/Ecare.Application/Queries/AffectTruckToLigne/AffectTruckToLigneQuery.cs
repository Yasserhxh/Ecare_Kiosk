using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.AffectTruckToLigne
{
    public sealed record AffectTruckToLigneQuery(string Produit,string Matricule,int BonDeCommande) : IRequest<string>;
     
    
}
