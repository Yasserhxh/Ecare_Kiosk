using MediatR;
using Ecare.Shared;

namespace Ecare.Application.Commands.Pab1Weigh;
public sealed record RecordPab1WeighCommand(string OrderNumber, int GrossKg) : IRequest<Result>;
