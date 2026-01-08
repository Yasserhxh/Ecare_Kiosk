using Dapper;
using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.GetLegendById
{
    public sealed class GetLegendByIdQueryHandler
    : IRequestHandler<GetLegendByIdQuery, Result<LegendFullVm>>
    {
        private readonly IUnitOfWork _uow;

        public GetLegendByIdQueryHandler(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<Result<LegendFullVm>> Handle(GetLegendByIdQuery request, CancellationToken ct)
        {
            if (request.Id <= 0)
                return Result<LegendFullVm>.Fail("Invalid Id.");

            await _uow.BeginAsync(ct);

            const string sql = @"
            SELECT
                  [Id]
                , [OrderId]
                , [CommercialOrderId]
                , [ClientName]
                , [Chantier]
                , [Matricule]
                , [RFIDCard]
                , [TypeCamion]
                , [ChauffeurName]
                , [CodeTransporteurSap]
                , [TransporteurName]
                , [NombrePlombs]
                , [Produit1]
                , [Quantite1]
                , [Produit2]
                , [Quantite2]
                , [TypeProduit]
                , [CodeClientSAP]
                , [CodeProduitSAP]
                , [PremierePoid]
                , [DeuxiemePoid]
                , [ParkingAt]
                , [ElapsedTimeParking]
                , [PabEntryAt]
                , [StartChargingAt]
                , [ElapsedInPab_Charging]
                , [FinishedChargingAt]
                , [ElapsedCharging]
                , [PabExitAt]
                , [ElapsedTimeInF_Exit]
                , [TotalTimeInCercuit]
                , [BonDeLivraison]
                , [Step]
                , [IsPined]
                , [PinedAt]
                , [AddedToQueueAt]
                , [FirstPlaceAt]
                , [TimeElapsedInFirstPlace]
                , [CreatedAt]
                , [BonDeCommande]
                , [Ligne]
                , [ChequeImg]
                , [ExtraSac]
                , [PlusBags]
                , [MinusBags]
                , [StartExtraSac]
                , [EndExtraSac]
                , [ElapsedExtraSac]
                , [UserId]
                , [CodeSapChantier]
                , [CodeSapClient]
                , [CodeSapCommande]
                , [CodeSapProduit1]
                , [CodeSapProduit2]
                , [SacNumber]
                , [NumberSacs_Charged]
                , [Weight_Charged]
                , [Status]
                , [LoadingStatus]
                , [PlombNumber]
                , [Plombs]
                , [SecondLigne]
                , [PermisDeConduite]
                , [PTAC]
                , [TARE]
                , [AnnulationCommercial]
                , [MotifAnnulationCommercial]
                , [UserIdAnnulationCommercial]
            FROM [dbo].[Ecare_Order_Legend]
            WHERE [Id] = @Id;
            ";

            try
            {
                var row = await _uow.Connection.QuerySingleOrDefaultAsync<LegendFullVm>(
                    new CommandDefinition(
                        sql,
                        new { request.Id },
                        transaction: _uow.Transaction,
                        cancellationToken: ct));

                await _uow.CommitAsync(ct);

                if (row is null)
                    return Result<LegendFullVm>.Fail($"Legend Id={request.Id} not found.");

                return Result<LegendFullVm>.Ok(row);
            }
            catch
            {
                await _uow.RollbackAsync(ct);
                throw;
            }
        }
    }
}
