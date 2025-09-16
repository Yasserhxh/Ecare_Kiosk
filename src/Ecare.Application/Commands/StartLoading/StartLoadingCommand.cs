using MediatR;
using Ecare.Shared;

namespace Ecare.Application.Commands;
public sealed record StartLoadingCommand(string OrderNumber, bool Validator1Ok, bool Validator2Ok) : IRequest<Result>;
