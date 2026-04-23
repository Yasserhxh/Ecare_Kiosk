using Dapper;
using Ecare.Shared;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecare.Application.Commands.EcareDriver;

public sealed class UpdateDriverHandler : IRequestHandler<UpdateDriverCommand, Result<bool>>
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<UpdateDriverHandler> _log;

    public UpdateDriverHandler(IUnitOfWork uow, ILogger<UpdateDriverHandler> log)
    {
        _uow = uow;
        _log = log;
    }

    public async Task<Result<bool>> Handle(UpdateDriverCommand request, CancellationToken ct)
    {
        const string selectSql = @"
SELECT TOP (1)
    COALESCE(
        NULLIF(LTRIM(RTRIM(Nom_Complet)), ''),
        NULLIF(LTRIM(RTRIM(CONCAT(ISNULL(Prenom, ''), ' ', ISNULL(Nom, '')))), '')
    ) AS FullName
FROM Ecare_Driver
WHERE Id = @Id;";

        const string updateSql = @"
UPDATE Ecare_Driver
SET Cin = @Cin,
    Nom = @Nom,
    Prenom = @Prenom,
    Numero = @Numero,
    Permis = @Permis,
    Nom_Complet = @NomComplet
WHERE Id = @Id;";

        try
        {
            await _uow.BeginAsync(ct);

            var previousDisplayName = await _uow.Connection.ExecuteScalarAsync<string?>(
                new CommandDefinition(selectSql, new { request.Id }, _uow.Transaction, cancellationToken: ct));

            if (string.IsNullOrWhiteSpace(previousDisplayName))
            {
                await _uow.RollbackAsync(ct);
                return Result<bool>.Fail("Driver not found");
            }

            var displayName = DriverWriteSupport.BuildDisplayName(request.NomComplet, request.Nom, request.Prenom);

            var affected = await _uow.Connection.ExecuteAsync(
                new CommandDefinition(
                    updateSql,
                    new
                    {
                        request.Id,
                        request.Cin,
                        request.Nom,
                        request.Prenom,
                        request.Numero,
                        request.Permis,
                        NomComplet = displayName
                    },
                    _uow.Transaction,
                    cancellationToken: ct));

            if (affected == 0)
            {
                await _uow.RollbackAsync(ct);
                return Result<bool>.Fail("Driver not found");
            }

            await DriverWriteSupport.SyncClientEquipementAsync(
                _uow,
                displayName,
                request.Permis,
                previousDisplayName,
                ct);

            await _uow.CommitAsync(ct);
            return Result<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            await _uow.RollbackAsync(ct);
            _log.LogError(ex, "Error updating driver {DriverId}", request.Id);
            return Result<bool>.Fail("Driver update failed");
        }
    }
}
