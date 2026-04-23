using Dapper;
using Ecare.Application.Commands.EcareDriver;
using Ecare.Shared;
using MediatR;
using System.Data;

namespace Ecare.Application.Queries.GetDrivers;

public sealed class GetDriverByIdHandler : IRequestHandler<GetDriverByIdQuery, Result<EcareDriver>>
{
    private readonly IDbConnectionFactory _factory;

    public GetDriverByIdHandler(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<Result<EcareDriver>> Handle(GetDriverByIdQuery request, CancellationToken ct)
    {
        const string sql = @"
SELECT
    Id,
    Cin,
    Nom,
    Prenom,
    Numero,
    Permis,
    COALESCE(
        NULLIF(LTRIM(RTRIM(Nom_Complet)), ''),
        NULLIF(LTRIM(RTRIM(CONCAT(ISNULL(Prenom, ''), ' ', ISNULL(Nom, '')))), '')
    ) AS NomComplet
FROM Ecare_Driver
WHERE Id = @Id;";

        using var conn = _factory.Create();
        if (conn.State != ConnectionState.Open)
            await ((dynamic)conn).OpenAsync(ct);

        var item = await conn.QuerySingleOrDefaultAsync<EcareDriver>(
            new CommandDefinition(sql, new { request.Id }, cancellationToken: ct));

        return item is null
            ? Result<EcareDriver>.Fail("Driver not found")
            : Result<EcareDriver>.Ok(item);
    }
}
