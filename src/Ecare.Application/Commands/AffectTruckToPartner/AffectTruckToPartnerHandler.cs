using Dapper;
using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.AffectTruckToPartner
{
    public sealed class AffectTruckToPartnerHandler
    : IRequestHandler<AffectTruckToPartnerCommand, Result<string>>
    {
        private readonly IUnitOfWork _uow;

        public AffectTruckToPartnerHandler(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<Result<string>> Handle(AffectTruckToPartnerCommand request, CancellationToken ct)
        {
            const string checkSql = @"
            SELECT TOP 1 TruckId
            FROM Ecare_Truck_Partner
            WHERE ClientId = @PartnerId;
        ";

            const string insertSql = @"
            INSERT INTO Ecare_Truck_Partner (ClientId, TruckId)
            VALUES (@PartnerId, @TruckId);
        ";

            try
            {
                await _uow.BeginAsync(ct);

                // Check if partner already has a truck
                var existingTruck = await _uow.Connection.ExecuteScalarAsync<int?>(
                    checkSql,
                    new { request.PartnerId },
                    _uow.Transaction
                );

                if (existingTruck.HasValue)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<string>.Fail("Partner already linked to another truck.");
                }

                // Insert new link
                await _uow.Connection.ExecuteAsync(
                    insertSql,
                    request,
                    _uow.Transaction
                );

                await _uow.CommitAsync(ct);

                return Result<string>.Ok("Truck assigned successfully.");
            }
            catch (Exception ex)
            {
                await _uow.RollbackAsync(ct);
                return Result<string>.Fail(ex.Message);
            }
        }
    }
}
