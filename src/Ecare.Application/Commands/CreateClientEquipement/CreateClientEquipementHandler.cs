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
                    sql,
                    request.Dto,
                    _uow.Transaction
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
