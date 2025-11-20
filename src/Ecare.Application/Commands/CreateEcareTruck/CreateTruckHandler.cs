using Dapper;
using Ecare.Application.Queries.GetDrivers;
using Ecare.Application.Queries.GetTruck;
using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.CreateEcareTruck
{
    public sealed class CreateTruckHandler
    : IRequestHandler<CreateTruckCommand, Result<int>>
    {
        private readonly IUnitOfWork _uow;

        public CreateTruckHandler(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<Result<int>> Handle(CreateTruckCommand request, CancellationToken ct)
        {
            const string sql = @"
            INSERT INTO Ecare_Truck
            (
                Matricule, PTAC, TARE, RfidCard,
                TruckTypeId, DriverId, NumberOfSeals
            )
            VALUES
            (
                @Matricule, @PTAC, @TARE, @RfidCard,
                @TruckTypeId, @DriverId, @NumberOfSeals
            );
            SELECT CAST(SCOPE_IDENTITY() AS INT);
        ";

            try
            {
                await _uow.BeginAsync(ct);

                var id = await _uow.Connection.ExecuteScalarAsync<int>(
                    sql,
                    request,
                    _uow.Transaction
                );

                await _uow.CommitAsync(ct);

                return Result<int>.Ok(id);
            }
            catch (Exception ex)
            {
                await _uow.RollbackAsync(ct);
                return Result<int>.Fail(ex.Message);
            }
        }
    }
}
