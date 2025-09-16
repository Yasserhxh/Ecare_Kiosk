using FluentValidation;
using Ecare.Application.Commands;

namespace Ecare.Application.Validators;
public sealed class ConfirmOrderCommandValidator : AbstractValidator<ConfirmOrderCommand>
{
    public ConfirmOrderCommandValidator()
    {
        RuleFor(x => x.OrderNumber).NotEmpty();
    }
}
