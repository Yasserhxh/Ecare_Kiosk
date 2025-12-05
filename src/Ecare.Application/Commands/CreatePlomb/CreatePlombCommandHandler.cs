using Dapper;
using Ecare.Shared;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.CreatePlomb
{
    public sealed class CreatePlombCommandHandler
    : IRequestHandler<CreatePlombCommand, bool>
    {
        private readonly IUnitOfWork _uow;
        private readonly ILogger<CreatePlombCommandHandler> _log;

        public CreatePlombCommandHandler(
            IUnitOfWork uow,
            ILogger<CreatePlombCommandHandler> log)
        {
            _uow = uow;
            _log = log;
        }

        public async Task<bool> Handle(CreatePlombCommand request, CancellationToken ct)
        {
            await _uow.BeginAsync(ct);

            const string sql = @"
            UPDATE dbo.Ecare_Order_Legend
            SET 
                PlombNumber = @PlombNumber,
                Plombs = @Plombs
            WHERE 
                RFIDCard = @RFIDCard
                AND Matricule = @Matricule
                AND Step < 5;
            ";

            var affected = await _uow.Connection.ExecuteAsync(sql, new
            {
                request.RFIDCard,
                request.Matricule,
                request.PlombNumber,
                request.Plombs
            }, _uow.Transaction);

            if (affected == 0)
            {
                _log.LogWarning("No rows updated for RFID={rfid} Matricule={mat}",
                    request.RFIDCard, request.Matricule);

                await _uow.RollbackAsync(ct);
                return false;
            }

            await _uow.CommitAsync(ct);
            return true;
        }
    }
}
