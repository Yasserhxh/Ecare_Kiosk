using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using Ecare.Domain.Entities;
using Ecare.Domain.Inerface;
using Ecare.Shared;
using MediatR;

namespace Ecare.Application.Engines.Create;

public sealed class CreateEcareEngineHandler
    : IRequestHandler<CreateEcareEngineCommand, Result<string>>
{
    private readonly IUnitOfWork _uow;
    private readonly IEcareEngineRepository _repo;

    public CreateEcareEngineHandler(IUnitOfWork uow, IEcareEngineRepository repo)
    {
        _uow = uow;
        _repo = repo;
    }

    public async Task<Result<string>> Handle(CreateEcareEngineCommand cmd, CancellationToken ct)
    {
        await _uow.BeginAsync(ct);

        try
        {
            var entity = new EcareEngine
            {
                Matricule = cmd.Matricule,
                PTAC = cmd.PTAC,
                TARE = cmd.TARE,
                Type_Camion = cmd.Type_Camion,
                CarteSlv = cmd.CarteSlv,
                Type_Process = cmd.Type_Process,
                Code_Process = cmd.Code_Process,
                Nom_Process = cmd.Nom_Process,
                Code_Chantier = cmd.Code_Chantier,
                Nom_Chantier = cmd.Nom_Chantier,
                Id_Chauffeur = cmd.Id_Chauffeur
            };

            var rows = await _repo.InsertAsync(entity, ct);

            if (rows != 1)
            {
                await _uow.RollbackAsync(ct);
                return Result<string>.Fail("Insert failed for Ecare_Engine.");
            }

            await _uow.CommitAsync(ct);
            return Result<string>.Ok(entity.Matricule);
        }
        catch (Exception ex)
        {
            await _uow.RollbackAsync(ct);
            return Result<string>.Fail(ex.Message);
        }
    }
}
