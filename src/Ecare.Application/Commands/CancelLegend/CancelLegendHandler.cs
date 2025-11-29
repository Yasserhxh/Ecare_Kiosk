using Dapper;
using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.CancelLegend
{
    public sealed class CancelLegendHandler : IRequestHandler<CancelLegendCommand, bool>
    {
        private readonly IUnitOfWork _uow;

        public CancelLegendHandler(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<bool> Handle(CancelLegendCommand request, CancellationToken ct)
        {
            // Start transaction
            await _uow.BeginAsync(ct);

            try
            {
                const string sql = @"
                UPDATE Ecare_Order_Legend
                SET Status = 'Canceled'
                WHERE Id = @Id";

                int affected = await _uow.Connection.ExecuteAsync(
                    sql,
                    new { Id = request.Id },
                    _uow.Transaction
                );

                if (affected == 0)
                {
                    await _uow.RollbackAsync(ct);
                    return false; // Legend not found
                }

                await _uow.CommitAsync(ct);
                return true;
            }
            catch
            {
                await _uow.RollbackAsync(ct);
                throw;
            }
        }
    }
}
