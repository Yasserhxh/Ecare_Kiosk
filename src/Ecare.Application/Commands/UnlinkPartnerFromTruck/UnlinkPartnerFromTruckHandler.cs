using Dapper;
using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.UnlinkPartnerFromTruck
{
    public sealed class UnlinkPartnerFromTruckHandler
     : IRequestHandler<UnlinkPartnerFromTruckCommand, Result<string>>
    {
        private readonly IUnitOfWork _uow;

        public UnlinkPartnerFromTruckHandler(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<Result<string>> Handle(UnlinkPartnerFromTruckCommand request, CancellationToken ct)
        {
            const string sql = @"
            DELETE FROM Ecare_Truck_Partner 
            WHERE ClientId = @PartnerId;
        ";

            try
            {
                await _uow.BeginAsync(ct);

                await _uow.Connection.ExecuteAsync(
                    sql,
                    request,
                    _uow.Transaction
                );

                await _uow.CommitAsync(ct);

                return Result<string>.Ok("Partner unlinked successfully.");
            }
            catch (Exception ex)
            {
                await _uow.RollbackAsync(ct);
                return Result<string>.Fail(ex.Message);
            }
        }
    }
}
