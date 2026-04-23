using Dapper;
using Ecare.Shared;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecare.Application.Commands.EcareDriver;

public sealed class DeleteDriverHandler : IRequestHandler<DeleteDriverCommand, Result<bool>>
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<DeleteDriverHandler> _log;

    public DeleteDriverHandler(IUnitOfWork uow, ILogger<DeleteDriverHandler> log)
    {
        _uow = uow;
        _log = log;
    }

    public async Task<Result<bool>> Handle(DeleteDriverCommand request, CancellationToken ct)
    {
        const string selectSql = @"
SELECT TOP (1)
    COALESCE(
        NULLIF(LTRIM(RTRIM(Nom_Complet)), ''),
        NULLIF(LTRIM(RTRIM(CONCAT(ISNULL(Prenom, ''), ' ', ISNULL(Nom, '')))), '')
    ) AS FullName,
    Permis
FROM Ecare_Driver
WHERE Id = @Id;";

        const string deleteSql = "DELETE FROM Ecare_Driver WHERE Id = @Id;";

        try
        {
            await _uow.BeginAsync(ct);

            var driver = await _uow.Connection.QuerySingleOrDefaultAsync<(string? FullName, string? Permis)>(
                new CommandDefinition(selectSql, new { request.Id }, _uow.Transaction, cancellationToken: ct));

            if (string.IsNullOrWhiteSpace(driver.FullName))
            {
                await _uow.RollbackAsync(ct);
                return Result<bool>.Fail("Driver not found");
            }

            var affected = await _uow.Connection.ExecuteAsync(
                new CommandDefinition(deleteSql, new { request.Id }, _uow.Transaction, cancellationToken: ct));

            if (affected == 0)
            {
                await _uow.RollbackAsync(ct);
                return Result<bool>.Fail("Driver not found");
            }

            await DriverWriteSupport.DeactivateClientEquipementAsync(
                _uow,
                driver.FullName.Trim(),
                driver.Permis,
                ct);

            await _uow.CommitAsync(ct);
            return Result<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            await _uow.RollbackAsync(ct);
            _log.LogError(ex, "Error deleting driver {DriverId}", request.Id);
            return Result<bool>.Fail("Driver delete failed");
        }
    }
}
