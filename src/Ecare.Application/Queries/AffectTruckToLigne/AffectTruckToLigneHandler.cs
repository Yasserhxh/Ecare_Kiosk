using Dapper;
using Ecare.Domain.ValueObjects;
using Ecare.Infrastructure.Repositories;
using Ecare.Shared;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ecare.Application.Queries.AffectTruckToLigne
{
    public class AffectTruckToLigneHandler : IRequestHandler<AffectTruckToLigneQuery, string>
    {
        private readonly ILogger<AffectTruckToLigneHandler> _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly EcareLigneRepository _repo;

        public AffectTruckToLigneHandler(
            ILogger<AffectTruckToLigneHandler> log,
            IUnitOfWork unitOfWork,
            EcareLigneRepository repo)
        {
            _log = log;
            _unitOfWork = unitOfWork;
            _repo = repo;
        }

        public async Task<string> Handle(AffectTruckToLigneQuery request, CancellationToken cancellationToken)
        {
            await _unitOfWork.BeginAsync(cancellationToken);

            var lignes = await _repo.GetLignesByTypeAndCimentAsync(request.Produit, _unitOfWork, cancellationToken);

            var best = lignes
                .Where(l => l.Status == (int)LigneStatus.Disponible)
                .OrderByDescending(l => l.RealtimeCapacity)
                .FirstOrDefault();

            if (best is null || best.RealtimeCapacity == 0)
            {
                

                const string update = @"UPDATE EcareFlux SET FirstWeight = NULL, PabEntryAt= NULL WHERE Matricule = @Matricule AND BonDeCommande=@BonDeCommande;";

                var up = await _unitOfWork.Connection.ExecuteAsync(
                    update,
                    new {Matricule = request.Matricule, BonDeCommande = request.BonDeCommande },
                    transaction: _unitOfWork.Transaction
                );

                await _unitOfWork.CommitAsync(cancellationToken);
                return "Aucune ligne disponible pour ce produit.";


            }

            

            //Update EcareFlux table
            const string updateSql = @"UPDATE EcareFlux SET Ligne = @LigneNom WHERE Matricule = @Matricule AND BonDeCommande=@BonDeCommande;";

            var affected = await _unitOfWork.Connection.ExecuteAsync(
                updateSql,
                new { LigneNom = best.LigneNom, Matricule = request.Matricule, BonDeCommande=request.BonDeCommande },
                transaction: _unitOfWork.Transaction
            );


            const string updateCapacitySql = @"
                UPDATE Ecare_Ligne
                SET RealtimeCapacity = CASE
                    WHEN ISNULL(RealtimeCapacity, 0) > 0 THEN ISNULL(RealtimeCapacity, 0) - 1
                    ELSE 0
                END
                WHERE Nom = @LigneNom;";

            await _unitOfWork.Connection.ExecuteAsync(
                updateCapacitySql,
                new { LigneNom = best.LigneNom },
                transaction: _unitOfWork.Transaction
            );

            await _unitOfWork.CommitAsync(cancellationToken);

            //_log.LogInformation("Flux {FluxId} mis à jour avec la ligne {LigneNom}", request.fluxId, best.LigneNom);

            return affected > 0
                ? $"{best.LigneNom}"
                : $"Aucune ligne mise à jour.";
        }
    }
}
