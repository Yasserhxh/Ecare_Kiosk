using Dapper;
using Ecare.Shared;

namespace Ecare.Application.Commands.EcareDriver;

internal static class DriverWriteSupport
{
    internal static string BuildDisplayName(string? nomComplet, string? nom, string? prenom)
    {
        if (!string.IsNullOrWhiteSpace(nomComplet))
            return nomComplet.Trim();

        var combined = $"{prenom ?? string.Empty} {nom ?? string.Empty}".Trim();
        if (!string.IsNullOrWhiteSpace(combined))
            return combined;

        return (nom ?? prenom ?? string.Empty).Trim();
    }

    internal static async Task SyncClientEquipementAsync(
        IUnitOfWork uow,
        string currentDisplayName,
        string? permis,
        string? previousDisplayName,
        CancellationToken ct)
    {
        const string sql = @"
DECLARE @ExistingId int;

SELECT TOP (1) @ExistingId = Id
FROM dbo.Ecare_ClientEquipements
WHERE ISNULL(IsDriver, 0) = 1
  AND (
        NULLIF(LTRIM(RTRIM(ChauffeurName)), '') = @CurrentDisplayName
        OR (@PreviousDisplayName IS NOT NULL AND NULLIF(LTRIM(RTRIM(ChauffeurName)), '') = @PreviousDisplayName)
        OR (@Permis IS NOT NULL AND NULLIF(LTRIM(RTRIM(PermisConducteur)), '') = @Permis)
      )
ORDER BY Id DESC;

IF @ExistingId IS NULL
BEGIN
    INSERT INTO dbo.Ecare_ClientEquipements
    (
        ChauffeurName,
        PermisConducteur,
        IsDriver,
        Status
    )
    VALUES
    (
        @CurrentDisplayName,
        @Permis,
        1,
        'ACTIVE'
    );
END
ELSE
BEGIN
    UPDATE dbo.Ecare_ClientEquipements
    SET ChauffeurName = @CurrentDisplayName,
        PermisConducteur = @Permis,
        IsDriver = 1,
        Status = COALESCE(Status, 'ACTIVE')
    WHERE Id = @ExistingId;
END;
";

        await uow.Connection.ExecuteAsync(
            new CommandDefinition(
                sql,
                new
                {
                    CurrentDisplayName = currentDisplayName,
                    PreviousDisplayName = string.IsNullOrWhiteSpace(previousDisplayName) ? null : previousDisplayName.Trim(),
                    Permis = string.IsNullOrWhiteSpace(permis) ? null : permis.Trim()
                },
                transaction: uow.Transaction,
                cancellationToken: ct));
    }

    internal static async Task DeactivateClientEquipementAsync(
        IUnitOfWork uow,
        string displayName,
        string? permis,
        CancellationToken ct)
    {
        const string sql = @"
UPDATE dbo.Ecare_ClientEquipements
SET Status = 'INACTIVE'
WHERE ISNULL(IsDriver, 0) = 1
  AND (
        NULLIF(LTRIM(RTRIM(ChauffeurName)), '') = @DisplayName
        OR (@Permis IS NOT NULL AND NULLIF(LTRIM(RTRIM(PermisConducteur)), '') = @Permis)
      );";

        await uow.Connection.ExecuteAsync(
            new CommandDefinition(
                sql,
                new
                {
                    DisplayName = displayName,
                    Permis = string.IsNullOrWhiteSpace(permis) ? null : permis.Trim()
                },
                transaction: uow.Transaction,
                cancellationToken: ct));
    }
}
