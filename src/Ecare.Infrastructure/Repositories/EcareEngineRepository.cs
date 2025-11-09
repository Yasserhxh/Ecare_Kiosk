using Dapper;
using Ecare.Domain.Entities;
using Ecare.Domain.Inerface;
using Ecare.Shared; // for IUnitOfWork
using System.Threading;
using System.Threading.Tasks;
// using Ecare.Shared; if IUnitOfWork is there

namespace Ecare.Infrastructure.Repositories;

public sealed class EcareEngineRepository : IEcareEngineRepository
{
    private readonly IUnitOfWork _uow;

    public EcareEngineRepository(IUnitOfWork uow)
        => _uow = uow;

    public async Task<int> InsertAsync(EcareEngine e, CancellationToken ct)
    {
        const string sql = """
        INSERT INTO dbo.Ecare_Engine
        (
            Matricule,
            PTAC,
            TARE,
            Type_Camion,
            CarteSlv,
            Type_Process,
            Code_Process,
            Nom_Process,
            Code_Chantier,
            Nom_Chantier,
            Id_Chauffeur
        )
        VALUES
        (
            @Matricule,
            @PTAC,
            @TARE,
            @Type_Camion,
            @CarteSlv,
            @Type_Process,
            @Code_Process,
            @Nom_Process,
            @Code_Chantier,
            @Nom_Chantier,
            @Id_Chauffeur
        );
        """;

        var param = new
        {
            e.Matricule,
            e.PTAC,
            e.TARE,
            e.Type_Camion,
            e.CarteSlv,
            Type_Process = (int?)e.Type_Process,
            e.Code_Process,
            e.Nom_Process,
            e.Code_Chantier,
            e.Nom_Chantier,
            e.Id_Chauffeur
        };

        return await _uow.Connection.ExecuteAsync(
            new CommandDefinition(sql, param, _uow.Transaction, cancellationToken: ct));
    }
}
