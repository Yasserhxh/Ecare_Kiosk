using MediatR;
using Ecare.Shared;

namespace Ecare.Application.Commands;
public sealed record CancelOrderCommand(string OrderNumber) : IRequest<Result>;
