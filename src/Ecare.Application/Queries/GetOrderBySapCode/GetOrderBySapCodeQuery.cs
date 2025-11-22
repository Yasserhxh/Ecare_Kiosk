using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.GetOrderBySapCode
{
    public sealed record GetOrderBySapCodeQuery(string CodeSapCommande)
    : IRequest<Result<IReadOnlyList<OrderBySapDto>>>;


    public sealed class OrderBySapDto
    {
        public int Id { get; set; }
        public string? ClientName { get; set; }
        public string? Chantier { get; set; }
        public string Matricule { get; set; } = "";
        public string? Produit1 { get; set; }
        public decimal? Quantite1 { get; set; }
        public string? Produit2 { get; set; }
        public decimal? Quantite2 { get; set; }
        public string? BonDeCommande { get; set; }
    }


}
