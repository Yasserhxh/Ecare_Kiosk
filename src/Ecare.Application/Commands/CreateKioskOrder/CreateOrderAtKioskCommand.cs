using MediatR;
using Ecare.Shared;

namespace Ecare.Application.Commands;
public sealed record CreateOrderAtKioskCommand(string Slv, int ProductId, int ProductType, string ProductName, string Unit, decimal Quantity)
    : IRequest<Result<Guid>>;


