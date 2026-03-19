using Dapper;
using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.UpdateCircuit.DeleteFirstPesage
{
    public class DeleteFirstPesageHandler : IRequestHandler<DeleteFirstPesageCommand, bool>
    {

        private readonly IUnitOfWork _uow;

        public DeleteFirstPesageHandler(IUnitOfWork uow)
        {
            _uow = uow;
        }
        public async Task<bool> Handle(DeleteFirstPesageCommand request, CancellationToken cancellationToken)
        {
            const string sqlUpdate = @"
            UPDATE Ecare_Order_Legend
            SET 
                Step = 1,
                PremierePoid = NULL,
                ElapsedTimeParking = NULL,
                Ligne=NULL,
                PabEntryAt = NULL,
                FirstPesageCanceledBy =@FirstPesageCanceledBy,
                FirstPesageCanceleAt = CONVERT(datetime, SYSDATETIMEOFFSET() AT TIME ZONE 'Morocco Standard Time')
            WHERE Id = @Id;";


            await _uow.BeginAsync(cancellationToken);

            try
            {
                await _uow.Connection.ExecuteAsync(
                    sqlUpdate,
                    new
                    {
                        request.Id,
                        request.FirstPesageCanceledBy
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
