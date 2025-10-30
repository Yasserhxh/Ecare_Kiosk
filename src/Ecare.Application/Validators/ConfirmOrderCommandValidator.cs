using FluentValidation;
using Ecare.Application.Commands.ConfirmOrder;

namespace Ecare.Application.Validators;
public sealed class ConfirmOrderCommandValidator : AbstractValidator<ConfirmOrderCommand>
{
    public ConfirmOrderCommandValidator()
    {
        RuleFor(x => x.OrderNumber).NotEmpty();
    }
}
