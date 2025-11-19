using Ecare.Shared;
using MediatR;

namespace Ecare.Application.Commands.LegendExtraSac;

public sealed record UpdateExtraSacCommand(
    int LegendId,
    int? PlusBags,
    int? MinusBags
) : IRequest<Result<bool>>;
