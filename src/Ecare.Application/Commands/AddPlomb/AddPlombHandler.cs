using Dapper;
using Ecare.Shared;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.AddPlomb
{
    public sealed class AddPlombHandler : IRequestHandler<AddPlombCommand, bool>
    {
        private readonly IUnitOfWork _uow;
        private readonly ILogger<AddPlombHandler> _log;

        public AddPlombHandler(IUnitOfWork uow, ILogger<AddPlombHandler> log)
        {
            _uow = uow;
            _log = log;
        }

        public async Task<bool> Handle(AddPlombCommand request, CancellationToken ct)
        {
            await _uow.BeginAsync(ct);

            const string sql = @"
            UPDATE dbo.Ecare_Order_Legend
            SET 
                PlombNumber = @PlombNumber,
                Plombs      = @Plombs
            WHERE RFIDCard = @RfidCard
              AND Matricule = @Matricule
              AND Step < 5;
            ";

            var rows = await _uow.Connection.ExecuteAsync(sql, new
            {
                request.PlombNumber,
                request.Plombs,
                request.RfidCard,
                request.Matricule
            }, _uow.Transaction);

            if (rows == 0)
            {
                _log.LogWarning("No order found for RFID {rfid} and Matricule {mat}", request.RfidCard, request.Matricule);
                await _uow.RollbackAsync(ct);
                return false;
            }

            await _uow.CommitAsync(ct);
            return true;
        }
    }
}
