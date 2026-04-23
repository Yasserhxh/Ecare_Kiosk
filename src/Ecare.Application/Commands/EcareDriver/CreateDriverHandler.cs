using Dapper;
using Ecare.Shared;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.EcareDriver
{
    public sealed class CreateDriverHandler
    : IRequestHandler<CreateDriverCommand, Result<int>>
    {
        private readonly IUnitOfWork _uow;
        private readonly ILogger<CreateDriverHandler> _log;

        public CreateDriverHandler(IUnitOfWork uow, ILogger<CreateDriverHandler> log)
        {
            _uow = uow;
            _log = log;
        }

        public async Task<Result<int>> Handle(CreateDriverCommand request, CancellationToken ct)
        {
            var displayName = DriverWriteSupport.BuildDisplayName(request.NomComplet, request.Nom, request.Prenom);

            const string sql = @"
            INSERT INTO Ecare_Driver (Cin, Nom, Prenom, Numero, Permis, Nom_Complet)
            VALUES (@Cin, @Nom, @Prenom, @Numero, @Permis, @NomComplet);

            SELECT CAST(SCOPE_IDENTITY() AS INT);
        ";

            try
            {
                await _uow.BeginAsync(ct);

                var id = await _uow.Connection.ExecuteScalarAsync<int>(
                    sql,
                    new
                    {
                        request.Cin,
                        request.Nom,
                        request.Prenom,
                        request.Numero,
                        request.Permis,
                        NomComplet = displayName
                    },
                    _uow.Transaction
                );

                await DriverWriteSupport.SyncClientEquipementAsync(
                    _uow,
                    displayName,
                    request.Permis,
                    previousDisplayName: null,
                    ct);

                await _uow.CommitAsync(ct);

                return Result<int>.Ok(id);
            }
            catch (Exception ex)
            {
                await _uow.RollbackAsync(ct);
                _log.LogError(ex, "Error creating driver");
                return Result<int>.Fail("Driver creation failed");
            }
        }
    }
}
