using MediatR;
using Ecare.Shared;

namespace Ecare.Application.Commands.CancelOrder;
public sealed record CancelOrderCommand(string OrderNumber) : IRequest<Result>;
