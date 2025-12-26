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
            UPDATE dbo.Ecare_Order_Legend
            SET
                AnnulationCommercial        = @AnnulationCommercial,
                MotifAnnulationCommercial   = @MotifAnnulationCommercial,
                UserIdAnnulationCommercial  = @UserIdAnnulationCommercial
            WHERE Id = @Id;

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
