using MediatR;
using Ecare.Shared;

namespace Ecare.Application.Commands;
public sealed record CreateLegacyOrderCommand(string NumeroCommande, string Slv, int ProductId, int? ProductId2, decimal Quantity, decimal? Quantity2, string Unite, string? Unite2)
    : IRequest<Result<int>>;  


