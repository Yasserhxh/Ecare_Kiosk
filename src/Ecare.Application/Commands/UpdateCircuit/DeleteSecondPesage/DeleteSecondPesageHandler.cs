using Azure.Core;
using Dapper;
using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.UpdateCircuit.DeleteSecondPesage
{
    public class DeleteSecondPesageHandler : IRequestHandler<DeleteSecondPesageCommand,bool>
    {
        private readonly IUnitOfWork _uow;

        public DeleteSecondPesageHandler(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<bool> Handle(DeleteSecondPesageCommand command,CancellationToken cancellationToken)
        {
            const string sqlUpdate = @"
            UPDATE Ecare_Order_Legend
            SET 
                Step = 4,
                DeuxiemePoid = NULL,
                ElapsedTimeParking = NULL,
                PabExitAt = NULL,
                ElapsedTimeInF_Exit = NULL,
                TotalTimeInCercuit = NULL,
                SecondPesageCanceledAt = CONVERT(datetime, SYSDATETIMEOFFSET() AT TIME ZONE 'Morocco Standard Time'),
                SecondPesageCanceledBy = @SecondPesageCanceledBy
            WHERE Id = @Id;";


            await _uow.BeginAsync(cancellationToken);

            try
            {
                await _uow.Connection.ExecuteAsync(
                    sqlUpdate,
                    new
                    {
                        command.Id,
                        command.SecondPesageCanceledBy
                    },
                    _uow.Transaction
                );

                await _uow.CommitAsync(cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                {
                    await _uow.RollbackAsync(cancellationToken);
                    return false;
                }
            }
        }
    }
}
