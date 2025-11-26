using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.Orders.UpdateOrder
{
    public sealed record UpdateOrderCommand(int OrderId, string ImageName) : IRequest<bool>;
}
