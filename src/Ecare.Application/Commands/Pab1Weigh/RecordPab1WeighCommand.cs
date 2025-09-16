using MediatR;
using Ecare.Shared;

namespace Ecare.Application.Commands;
public sealed record RecordPab1WeighCommand(string OrderNumber, int GrossKg) : IRequest<Result>;
