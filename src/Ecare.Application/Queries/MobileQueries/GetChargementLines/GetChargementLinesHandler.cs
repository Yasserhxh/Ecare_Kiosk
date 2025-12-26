using Dapper;
using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.MobileQueries.GetChargementLines
{
    public sealed class GetChargementLinesByTypeHandler
    : IRequestHandler<GetChargementLinesByTypeQuery, Result<IReadOnlyList<ChargementLineVm>>>
    {
        private readonly IUnitOfWork _uow;
        public GetChargementLinesByTypeHandler(IUnitOfWork uow) => _uow = uow;

        public async Task<Result<IReadOnlyList<ChargementLineVm>>> Handle(GetChargementLinesByTypeQuery request, CancellationToken ct)
        {
            const string sql = @"
            SELECT 
                EL.Nom
            FROM dbo.Ecare_Ligne AS EL
            JOIN dbo.Ecare_Zone_Chargement AS EZC ON EL.ZoneChargementId = EZC.Id
            WHERE EZC.TypeOperation = @TypeOperation;";

            try
            {
                await _uow.BeginAsync(ct);

                var lines = (await _uow.Connection.QueryAsync<ChargementLineVm>(
                    new CommandDefinition(sql, new { request.TypeOperation }, _uow.Transaction, cancellationToken: ct)))
                    .ToList();

                await _uow.CommitAsync(ct);
                return Result<IReadOnlyList<ChargementLineVm>>.Ok(lines);
            }
            catch (Exception ex)
            {
                await _uow.RollbackAsync(ct);
                return Result<IReadOnlyList<ChargementLineVm>>.Fail($"Query failed: {ex.Message}");
            }
        }
    }
}
