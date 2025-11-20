using Dapper;
using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.CreateEcareTruckType
{
    public sealed class CreateTruckTypeHandler
    : IRequestHandler<CreateTruckTypeCommand, Result<int>>
    {
        private readonly IUnitOfWork _uow;

        public CreateTruckTypeHandler(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<Result<int>> Handle(CreateTruckTypeCommand request, CancellationToken ct)
        {
            const string sql = @"
            INSERT INTO Ecare_Truck_Type (Type)
            VALUES (@Type);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
        ";

            try
            {
                await _uow.BeginAsync(ct);

                var id = await _uow.Connection.ExecuteScalarAsync<int>(
                    sql,
                    new { request.Type },
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
