using Dapper;
using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.NewCard.NewClientEquipment
{
    public sealed class SaveClientEquipementHandler(IUnitOfWork uow)
    : IRequestHandler<SaveClientEquipementCommand, SaveClientEquipementResult>
    {
        public async Task<SaveClientEquipementResult> Handle(
            SaveClientEquipementCommand request,
            CancellationToken ct)
        {
            await uow.BeginAsync(ct);

            try
            {
                var dto = request.Data;
                var carteSlv = dto.CarteSLV?.Trim();
                var rfidHex = dto.RfidHex?.Trim();

                const string duplicateEquipSql = @"
                SELECT TOP(1) Id
                FROM dbo.Ecare_ClientEquipements
                WHERE (ISNULL(IsClient, 0) = 1 OR ISNULL(IsTransporteur, 0) = 1)
                  AND (
                      (NULLIF(@CarteSLV, '') IS NOT NULL AND LTRIM(RTRIM(CarteSLV)) = @CarteSLV)
                      OR
                      (NULLIF(@RfidHex, '') IS NOT NULL AND LTRIM(RTRIM(RfidHex)) = @RfidHex)
                  );";

                var duplicateEquipId = await uow.Connection.QuerySingleOrDefaultAsync<int?>(
                    new CommandDefinition(
                        duplicateEquipSql,
                        new { CarteSLV = carteSlv, RfidHex = rfidHex },
                        uow.Transaction,
                        cancellationToken: ct));

                if (duplicateEquipId.HasValue)
                    throw new InvalidOperationException($"La carte SLV {carteSlv} existe deja comme carte permanente.");

                // 1) Ensure the physical tag exists before creating a permanent card.
                if (!string.IsNullOrWhiteSpace(carteSlv) && !string.IsNullOrWhiteSpace(rfidHex))
                {
                    const string findTagSql = @"
                    SELECT TOP(1) Id
                    FROM dbo.Ecare_Tags
                    WHERE LTRIM(RTRIM(CarteSLV)) = @CarteSLV
                       OR LTRIM(RTRIM(RfidHex)) = @RfidHex;";

                    var tagId = await uow.Connection.QuerySingleOrDefaultAsync<int?>(
                        new CommandDefinition(
                            findTagSql,
                            new { CarteSLV = carteSlv, RfidHex = rfidHex },
                            uow.Transaction,
                            cancellationToken: ct));

                    if (tagId.HasValue)
                        throw new InvalidOperationException($"La carte SLV {carteSlv} existe deja comme carte provisoire.");

                    const string insertTagSql = @"
                    INSERT INTO dbo.Ecare_Tags (CarteSLV, RfidHex)
                    VALUES (@CarteSLV, @RfidHex);";

                    await uow.Connection.ExecuteAsync(
                        new CommandDefinition(insertTagSql,
                            new { CarteSLV = carteSlv, RfidHex = rfidHex },
                            uow.Transaction,
                            cancellationToken: ct));
                }

                // 2) Insert equipement (all nullable columns OK)
                const string insertEquipSql = @"
                INSERT INTO dbo.Ecare_ClientEquipements
                (
                    ClientName,
                    CarteSLV,
                    Matricule,
                    ChauffeurName,
                    RfidHex,
                    CodeClientSAP,
                    Type,
                    PlombsNumber,
                    PTAC,
                    TARE,
                    Status,
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
                    @Type,
                    @PlombsNumber,
                    @PTAC,
                    @TARE,
                    @Status,
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

                SELECT CAST(SCOPE_IDENTITY() AS int);";

                var newId = await uow.Connection.QuerySingleAsync<int>(
                    new CommandDefinition(insertEquipSql, dto, uow.Transaction, cancellationToken: ct));

                await uow.CommitAsync(ct);
                return new SaveClientEquipementResult(newId);
            }
            catch
            {
                await uow.RollbackAsync(ct);
                throw;
            }
        }

    }
}
