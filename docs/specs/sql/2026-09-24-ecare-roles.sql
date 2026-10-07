-- Adds the 3 new Ecare profil roles (Admin IT already exists) and links all four
-- to the Ciments activité so they appear in mycimar's GetRolesParActivité dropdown.
-- Idempotent. Run against the shared DB (sqldb-emea-we-dssprod-dss-001). No EF migration.

SET NOCOUNT ON;

DECLARE @roles TABLE (Name NVARCHAR(256));
INSERT INTO @roles (Name) VALUES (N'Agent de Guichet'), (N'Logistique'), (N'Expédition');

INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp)
SELECT LOWER(CONVERT(NVARCHAR(36), NEWID())), r.Name, UPPER(r.Name), CONVERT(NVARCHAR(36), NEWID())
FROM @roles r
WHERE NOT EXISTS (SELECT 1 FROM AspNetRoles a WHERE a.NormalizedName = UPPER(r.Name));

-- Link the four profil roles to the Ciments activité (if the Activite/link tables exist).
DECLARE @ciment INT = (SELECT TOP 1 Id FROM Activite WHERE Service LIKE N'Ciment%');

IF @ciment IS NOT NULL
BEGIN
    INSERT INTO AspNetRoles_Activités (Role_Id, Activité_Id)
    SELECT a.Id, @ciment
    FROM AspNetRoles a
    WHERE a.NormalizedName IN (UPPER(N'Agent de Guichet'), UPPER(N'Logistique'),
                               UPPER(N'Expédition'), UPPER(N'Admin IT'))
      AND NOT EXISTS (SELECT 1 FROM AspNetRoles_Activités x
                      WHERE x.Role_Id = a.Id AND x.Activité_Id = @ciment);
END
