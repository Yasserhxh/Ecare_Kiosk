using Dapper;
using Ecare.Shared;
using MediatR;
using System.Data;


namespace Ecare.Application.Commands.UpdateCommercialAnnulation
{
    public sealed class UpdateCommercialAnnulationHandler
     : IRequestHandler<UpdateCommercialAnnulationCommand, Result<UpdateCommercialAnnulationVm>>
    {
        private readonly IUnitOfWork _uow;

        public UpdateCommercialAnnulationHandler(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<Result<UpdateCommercialAnnulationVm>> Handle(
            UpdateCommercialAnnulationCommand request,
            CancellationToken ct)
        {
            if (request.Id <= 0)
                return Result<UpdateCommercialAnnulationVm>.Fail("Invalid Id.");

            if (request.MotifAnnulationCommercial is { Length: > 450 })
                return Result<UpdateCommercialAnnulationVm>.Fail("MotifAnnulationCommercial max length is 450.");

            if (request.UserIdAnnulationCommercial is { Length: > 255 })
                return Result<UpdateCommercialAnnulationVm>.Fail("UserIdAnnulationCommercial max length is 255.");

            await _uow.BeginAsync(ct);

            const string sql = @"
            DECLARE @CurrentStep INT;
            DECLARE @CurrentLigne NVARCHAR(150);
            DECLARE @HasFirstPesage BIT = 0;
            DECLARE @ShouldReleaseCapacity BIT = 0;
            DECLARE @LegendUpdated BIT = 0;

            SELECT
                @CurrentStep = ISNULL(Step, 0),
                @CurrentLigne = Ligne,
                @HasFirstPesage =
                    CASE
                        WHEN PremierePoid IS NOT NULL OR PabEntryAt IS NOT NULL THEN 1
                        ELSE 0
                    END
            FROM dbo.Ecare_Order_Legend
            WHERE Id = @Id;

            SET @ShouldReleaseCapacity =
                CASE
                    WHEN ISNULL(@AnnulationCommercial, 0) = 1
                         AND @HasFirstPesage = 1
                         AND @CurrentStep >= 2
                         AND @CurrentStep < 5
                         AND @CurrentLigne IS NOT NULL
                         AND LTRIM(RTRIM(@CurrentLigne)) <> ''
                    THEN 1
                    ELSE 0
                END;

            UPDATE dbo.Ecare_Order_Legend
            SET
                AnnulationCommercial        = @AnnulationCommercial,
                MotifAnnulationCommercial   = @MotifAnnulationCommercial,
                UserIdAnnulationCommercial  = @UserIdAnnulationCommercial,
                Step                        = CASE
                                                WHEN ISNULL(@AnnulationCommercial, 0) = 1 AND ISNULL(Step, 0) < 5 THEN 5
                                                ELSE Step
                                              END,
                Status                      = CASE
                                                WHEN ISNULL(@AnnulationCommercial, 0) = 1 THEN 'Canceled'
                                                ELSE Status
                                              END
            WHERE Id = @Id;

            SET @LegendUpdated = CASE WHEN @@ROWCOUNT > 0 THEN 1 ELSE 0 END;

            IF @LegendUpdated = 1 AND @ShouldReleaseCapacity = 1
            BEGIN
                UPDATE L
                SET L.RealtimeCapacity =
                    CASE
                        WHEN ISNULL(L.RealtimeCapacity, 0) < ISNULL(L.Capacity, 0)
                            THEN ISNULL(L.RealtimeCapacity, 0) + 1
                        ELSE ISNULL(L.Capacity, 0)
                    END
                FROM dbo.Ecare_Ligne L
                WHERE L.Nom = @CurrentLigne;
            END;

            IF @LegendUpdated = 1 AND ISNULL(@AnnulationCommercial, 0) = 1
            BEGIN
                UPDATE O
                SET O.Statut = 'Annulee'
                FROM dbo.Orders O
                INNER JOIN dbo.Ecare_Order_Legend L ON L.OrderId = O.Id
                WHERE L.Id = @Id;
            END;

            SELECT
                Id,
                AnnulationCommercial,
                MotifAnnulationCommercial,
                UserIdAnnulationCommercial
            FROM dbo.Ecare_Order_Legend
            WHERE Id = @Id;
            ";

            try
            {
                var vm = await _uow.Connection.QuerySingleOrDefaultAsync<UpdateCommercialAnnulationVm>(
                    new CommandDefinition(
                        sql,
                        new
                        {
                            request.Id,
                            request.AnnulationCommercial,
                            request.MotifAnnulationCommercial,
                            request.UserIdAnnulationCommercial
                        },
                        transaction: _uow.Transaction,
                        cancellationToken: ct));

                if (vm is null)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<UpdateCommercialAnnulationVm>.Fail($"Legend Id={request.Id} not found.");
                }

                await _uow.CommitAsync(ct);
                return Result<UpdateCommercialAnnulationVm>.Ok(vm);
            }
            catch
            {
                await _uow.RollbackAsync(ct);
                throw;
            }
        }
    }
}
