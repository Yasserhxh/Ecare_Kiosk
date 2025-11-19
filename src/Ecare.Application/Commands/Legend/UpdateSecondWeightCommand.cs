using Ecare.Shared;
using MediatR;

namespace Ecare.Application.Commands.Legend;

public sealed record UpdateSecondWeightCommand(
    int RfidCard,
    string Matricule,
    int DeuxiemePoid
) : IRequest<Result<UpdateSecondWeightResult>>;

public sealed class UpdateSecondWeightResult
{
    public bool Success { get; set; }
    public int? UpdatedOrderId { get; set; }
}
