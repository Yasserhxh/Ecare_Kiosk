using Ecare.Shared;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.Queue.CreateQueue
{
    public sealed class CreateQueueEntryHandler(IUnitOfWork uow)
        : IRequestHandler<CreateQueueEntryCommand, Result<int>>
    {
        
            public async Task<Result<int>> Handle(CreateQueueEntryCommand request, CancellationToken ct)
            {
                await uow.BeginAsync(ct);
                try
                {
                    var newId = await EcareQueueWriter.InsertAsync(
                        uow,
                        request.Matricule,
                        request.NomChauffeur,
                        request.Qualite1,
                        request.Qualite2,
                        request.Quantite1,
                        request.Quantite2,
                        request.BonCommande,
                        request.BonLivraison,
                        request.Source,
                        request.Status,
                        request.CreatedAt,
                        ct: ct);

                    await uow.CommitAsync(ct);
                    return Result<int>.Ok(newId);
                }
                catch
                {
                    await uow.RollbackAsync(ct);
                    throw;
                }
            }
    }
    
}
