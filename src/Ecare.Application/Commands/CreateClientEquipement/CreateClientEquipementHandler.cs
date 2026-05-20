using Dapper;
using MediatR;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.CreateClientEquipement
{
    using Dapper;
    using MediatR;
    using Ecare.Shared;

    public sealed class CreateClientEquipementHandler
        : IRequestHandler<CreateClientEquipementCommand, int>
    {
        private readonly IUnitOfWork _uow;

        public CreateClientEquipementHandler(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<int> Handle(CreateClientEquipementCommand request, CancellationToken ct)
        {
            await _uow.BeginAsync(ct);

            try
            {
                var dto = request.Dto;
                var carteSlv = dto.CarteSLV?.Trim();
                var rfidHex = dto.RfidHex?.Trim();

                const string duplicateSql = @"
                SELECT TOP(1) Id
                FROM dbo.Ecare_ClientEquipements
                WHERE (ISNULL(IsClient, 0) = 1 OR ISNULL(IsTransporteur, 0) = 1)
                  AND (
                      (NULLIF(@CarteSLV, '') IS NOT NULL AND LTRIM(RTRIM(CarteSLV)) = @CarteSLV)
                      OR
                      (NULLIF(@RfidHex, '') IS NOT NULL AND LTRIM(RTRIM(RfidHex)) = @RfidHex)
                  );";

                var duplicateId = await _uow.Connection.QuerySingleOrDefaultAsync<int?>(
                    new CommandDefinition(
                        duplicateSql,
                        new { CarteSLV = carteSlv, RfidHex = rfidHex },
                        _uow.Transaction,
                        cancellationToken: ct));

                if (duplicateId.HasValue)
                    throw new InvalidOperationException($"La carte SLV {carteSlv} existe deja comme carte permanente.");

                if (!string.IsNullOrWhiteSpace(carteSlv) && !string.IsNullOrWhiteSpace(rfidHex))
                {
                    const string findTagSql = @"
                    SELECT TOP(1) Id
                    FROM dbo.Ecare_Tags
                    WHERE LTRIM(RTRIM(CarteSLV)) = @CarteSLV
                       OR LTRIM(RTRIM(RfidHex)) = @RfidHex;";

                    var tagId = await _uow.Connection.QuerySingleOrDefaultAsync<int?>(
                        new CommandDefinition(
                            findTagSql,
                            new { CarteSLV = carteSlv, RfidHex = rfidHex },
                            _uow.Transaction,
                            cancellationToken: ct));

                    // If the tag already exists as provisoire, reuse it (skip insert).
                    // This is the normal upgrade path: provisoire → permanente.
                    if (!tagId.HasValue)
                    {
                        const string insertTagSql = @"
                        INSERT INTO dbo.Ecare_Tags (CarteSLV, RfidHex)
                        VALUES (@CarteSLV, @RfidHex);";

                        await _uow.Connection.ExecuteAsync(
                            new CommandDefinition(
                                insertTagSql,
                                new { CarteSLV = carteSlv, RfidHex = rfidHex },
                                _uow.Transaction,
                                cancellationToken: ct));
                    }
                }

                const string sql = @"
                INSERT INTO [dbo].[Ecare_ClientEquipements]
                (
                    ClientName,
                    CarteSLV,
                    Matricule,
                    ChauffeurName,
                    RfidHex,
                    CodeClientSAP,
                    PlombsNumber,
                    PTAC,
                    TARE,
                    CodeTransporteurSap,
                    TransporteurName,
                    CodeTruckSap,
                    CodeTransporteurSapCimar,
                    PermisConducteur,
                    IsClient,
                    IsTransporteur,
                    IsDriver,
                    TruckType
                )
                VALUES
                (
                    @ClientName,
                    @CarteSLV,
                    @Matricule,
                    @ChauffeurName,
                    @RfidHex,
                    @CodeClientSAP,
                    @PlombsNumber,
                    @PTAC,
                    @TARE,
                    @CodeTransporteurSap,
                    @TransporteurName,
                    @CodeTruckSap,
                    @CodeTransporteurSapCimar,
                    @PermisConducteur,
                    @IsClient,
                    @IsTransporteur,
                    @IsDriver,
                    @TruckType
                );

                SELECT CAST(SCOPE_IDENTITY() as int);";

                var id = await _uow.Connection.ExecuteScalarAsync<int>(
                    new CommandDefinition(sql, dto, _uow.Transaction, cancellationToken: ct)
                );

                await _uow.CommitAsync(ct);
                return id;
            }
            catch
            {
                await _uow.RollbackAsync(ct);
                throw;
            }
        }
    }

}
