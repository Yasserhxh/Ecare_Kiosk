using FluentValidation;
using Ecare.Application.Commands;

namespace Ecare.Application.Validators;
public sealed class RecordPab1WeighValidator : AbstractValidator<RecordPab1WeighCommand>
{
    public RecordPab1WeighValidator()
    {
        RuleFor(x => x.OrderNumber).NotEmpty();
        RuleFor(x => x.GrossKg).GreaterThan(0);
    }
}
