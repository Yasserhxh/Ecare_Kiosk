using Dapper;
using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.CreateChantier
{
    public sealed class CreateChantierHandler
    : IRequestHandler<CreateChantierCommand, Result<int>>
    {
        private readonly IUnitOfWork _uow;

        public CreateChantierHandler(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<Result<int>> Handle(CreateChantierCommand request, CancellationToken ct)
        {
            const string sql = @"
            INSERT INTO Ecare_Chantier
            (
                Code,
                Name,
                ClientId,
                DateCreation,
                Actif
            )
            VALUES
            (
                @Code,
                @Name,
                @PartnerId,
                GETDATE(),
                @Actif
            );

            SELECT CAST(SCOPE_IDENTITY() AS INT);
        ";

            try
            {
                await _uow.BeginAsync(ct);

                var id = await _uow.Connection.ExecuteScalarAsync<int>(
                    sql,
                    new
                    {
                        request.Code,
                        request.Name,
                        PartnerId = request.PartnerId,
                        request.Actif
                    },
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
