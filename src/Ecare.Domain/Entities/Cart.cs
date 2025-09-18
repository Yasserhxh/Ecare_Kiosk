using System;

namespace Ecare.Domain.Entities;

public class Cart
{
    public int Id { get; set; }
    public int ProduitId { get; set; }
    public string NomProduit { get; set; } = default!;
    public decimal Quantite { get; set; }
    public string? Unite { get; set; }
    public DateTime DateAjout { get; set; }
    public Guid UserId { get; set; }
    public Guid? ShippingId { get; set; }
}
