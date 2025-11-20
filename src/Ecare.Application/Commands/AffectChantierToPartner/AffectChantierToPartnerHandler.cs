using Dapper;
using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.AffectChantierToPartner
{
    public sealed class AffectChantierToPartnerHandler
    : IRequestHandler<AffectChantierToPartnerCommand, Result<string>>
    {
        private readonly IUnitOfWork _uow;

        public AffectChantierToPartnerHandler(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<Result<string>> Handle(AffectChantierToPartnerCommand request, CancellationToken ct)
        {
            const string checkChantierSql = @"
            SELECT TOP 1 Id
            FROM Ecare_Chantier
            WHERE Id = @ChantierId;
        ";

            const string updateSql = @"
            UPDATE Ecare_Chantier
            SET ClientId = @PartnerId
            WHERE Id = @ChantierId;
        ";

            try
            {
                await _uow.BeginAsync(ct);

                // 1. Check if chantier exists
                var exists = await _uow.Connection.ExecuteScalarAsync<int?>(
                    checkChantierSql,
                    new { request.ChantierId },
                    _uow.Transaction
                );

                if (!exists.HasValue)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<string>.Fail("Chantier not found.");
                }

                // 2. Update or override partner assignment
                await _uow.Connection.ExecuteAsync(
                    updateSql,
                    request,
                    _uow.Transaction
                );

                await _uow.CommitAsync(ct);

                return Result<string>.Ok("Chantier successfully assigned to partner.");
            }
            catch (Exception ex)
            {
                await _uow.RollbackAsync(ct);
                return Result<string>.Fail(ex.Message);
            }
        }
    }
}
