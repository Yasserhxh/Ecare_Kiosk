using MediatR;
namespace Ecare.Application.Commands.ModifyLegend;
public class ModifyLegendQuantitiesCommand : IRequest<ModifyLegendQuantitiesResult>
{
    public int OrderLegendId { get; set; }

    public decimal? OldQuantite1 { get; set; }
    public decimal? OldQuantite2 { get; set; }

    public decimal? NewQuantite1 { get; set; }
    public decimal? NewQuantite2 { get; set; }

    public int? NewSacNumber { get; set; }
}
