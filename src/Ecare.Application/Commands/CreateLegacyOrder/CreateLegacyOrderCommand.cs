using MediatR;
using Ecare.Shared;

namespace Ecare.Application.Commands;
public sealed record CreateLegacyOrderCommand(int NumeroCommande, string Slv, int ProductId, decimal Quantity, string Unite)
    : IRequest<Result<string>>; // returns NumeroCommande


