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

                // 1) Upsert tag by RfidHex (only if RfidHex provided)
                if (!string.IsNullOrWhiteSpace(dto.RfidHex))
                {
                    const string findTagSql = @"
                    SELECT TOP(1) Id, CarteSLV, RfidHex
                    FROM dbo.Ecare_Tags
                    WHERE RfidHex = @RfidHex;";

                    var tag = await uow.Connection.QuerySingleOrDefaultAsync<TagRow>(
                        new CommandDefinition(findTagSql, new { RfidHex = dto.RfidHex }, uow.Transaction, cancellationToken: ct));

                    if (tag is null)
                    {
                        const string insertTagSql = @"
                        INSERT INTO dbo.Ecare_Tags (CarteSLV, RfidHex)
                        VALUES (@CarteSLV, @RfidHex);";

                        await uow.Connection.ExecuteAsync(
                            new CommandDefinition(insertTagSql,
                                new { CarteSLV = dto.CarteSLV, RfidHex = dto.RfidHex },
                                uow.Transaction,
                                cancellationToken: ct));
                    }
                    else
                    {
                        // if CarteSLV is provided and differs -> update
                        if (!string.IsNullOrWhiteSpace(dto.CarteSLV) &&
                            !string.Equals(tag.CarteSLV, dto.CarteSLV, StringComparison.OrdinalIgnoreCase))
                        {
                            const string updateTagSql = @"
                            UPDATE dbo.Ecare_Tags
                            SET CarteSLV = @CarteSLV
                            WHERE Id = @Id;";

                            await uow.Connection.ExecuteAsync(
                                new CommandDefinition(updateTagSql,
                                    new { Id = tag.Id, CarteSLV = dto.CarteSLV },
                                    uow.Transaction,
                                    cancellationToken: ct));
                        }
                    }
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

        private sealed class TagRow
        {
            public int Id { get; init; }
            public string? CarteSLV { get; init; }
            public string? RfidHex { get; init; }
        }
    }
}
