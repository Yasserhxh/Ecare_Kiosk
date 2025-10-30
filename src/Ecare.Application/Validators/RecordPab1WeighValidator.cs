using FluentValidation;
using Ecare.Application.Commands.Pab1Weigh;

namespace Ecare.Application.Validators;
public sealed class RecordPab1WeighValidator : AbstractValidator<RecordPab1WeighCommand>
{
    public RecordPab1WeighValidator()
    {
        RuleFor(x => x.OrderNumber).NotEmpty();
        RuleFor(x => x.GrossKg).GreaterThan(0);
    }
}
