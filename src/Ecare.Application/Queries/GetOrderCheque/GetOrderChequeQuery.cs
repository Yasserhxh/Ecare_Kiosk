using Ecare.Domain.ValueObjects;
using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.GetOrderCheque
{
    public sealed record GetOrderChequesPagedQuery(int Page, int PageSize)
    : IRequest<Result<OrderChequesPageVm>>;
}
