using Dapper;
using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.Partner
{
    public sealed class CreatePartnerHandler
    : IRequestHandler<CreatePartnerCommand, Result<int>>
    {
        private readonly IUnitOfWork _uow;

        public CreatePartnerHandler(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<Result<int>> Handle(CreatePartnerCommand request, CancellationToken ct)
        {
            const string sql = @"
            INSERT INTO Ecare_Partner
            (Code, Name, PartnerType, DateCreation, Actif)
            VALUES
            (@Code, @Name, @PartnerType, GETDATE(), 1);

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
