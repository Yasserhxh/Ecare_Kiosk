using Dapper;
using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.MobileCommands
{
    public sealed class UpdateFluxBagsHandler : IRequestHandler<UpdateFluxBagsCommand, Result<int>>
    {
        private readonly IUnitOfWork _uow;
        public UpdateFluxBagsHandler(IUnitOfWork uow) => _uow = uow;

        public async Task<Result<int>> Handle(UpdateFluxBagsCommand request, CancellationToken ct)
        {
            const string sql = @"
                UPDATE dbo.Ecare_Order_Legend
                SET MinusBags = @MinusBag,
                    PlusBags  = @PlusBag
                WHERE Id = @Id;";

            try
            {
                await _uow.BeginAsync(ct);

                var rows = await _uow.Connection.ExecuteAsync(
                    new CommandDefinition(sql, new
                    {
                        request.Id,
                        request.MinusBag,
                        request.PlusBag
                    },
                    transaction: _uow.Transaction,
                    cancellationToken: ct));

                await _uow.CommitAsync(ct);

                return Result<int>.Ok(rows);
            }
            catch (Exception ex)
            {
                await _uow.RollbackAsync(ct);
                return Result<int>.Fail($"Update failed: {ex.Message}");
            }
        }
    }
}
