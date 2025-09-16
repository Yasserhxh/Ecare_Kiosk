using Ecare.Domain.ValueObjects;

namespace Ecare.Domain.Entities;
public class Driver
{
    public Guid Id { get; init; }
    public SlvId Slv { get; init; }
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string Plate { get; set; } = default!;
    public Guid? ClientId { get; set; }
}
