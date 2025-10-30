using MediatR;
using Ecare.Shared;

namespace Ecare.Application.Commands.Pab2WeighAndBl;
public sealed record RecordPab2AndIssueBlCommand(string OrderNumber, int GrossKg, int TareKg, int TolerancePct) : IRequest<Result<string>>;
