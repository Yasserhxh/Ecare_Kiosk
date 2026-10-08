-- 2026-10-08 — Six equivalent Ecare profil roles + user assignment (Casablanca org chart).
-- Each new role reuses an existing profil's permission set (see PermissionMatrix.Equivalences):
--   Coordinateur Commercial        = Agent de Guichet
--   Chef d'Agence Logistique       = Logistique
--   Superviseur Ensachage          = Expédition
--   Chef d'équipe Ensachage        = Expédition
--   Surveillant de quai Ensachage  = Expédition
--   Opérateur vrac Ensachage       = Expédition
-- Idempotent. Run against the shared DB (sqldb-emea-we-dssprod-dss-001). No EF migration.
-- Role names must match EcareRoles.cs byte for byte (straight apostrophe ').
-- Prerequisite: docs/specs/sql/2026-09-24-ecare-roles.sql (base profils).

SET NOCOUNT ON;

-- 1) Roles -------------------------------------------------------------------
DECLARE @roles TABLE (Name NVARCHAR(256));
INSERT INTO @roles (Name) VALUES
    (N'Coordinateur Commercial'),
    (N'Chef d''Agence Logistique'),
    (N'Superviseur Ensachage'),
    (N'Chef d''équipe Ensachage'),
    (N'Surveillant de quai Ensachage'),
    (N'Opérateur vrac Ensachage');

INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp)
SELECT LOWER(CONVERT(NVARCHAR(36), NEWID())), r.Name, UPPER(r.Name), CONVERT(NVARCHAR(36), NEWID())
FROM @roles r
WHERE NOT EXISTS (SELECT 1 FROM AspNetRoles a WHERE a.NormalizedName = UPPER(r.Name));

-- 2) Link to the Ciments activité so they show in mycimar's role dropdown ----
DECLARE @ciment INT = (SELECT TOP 1 Id FROM Activite WHERE Service LIKE N'Ciment%');

IF @ciment IS NOT NULL
BEGIN
    INSERT INTO AspNetRoles_Activités (Role_Id, Activité_Id)
    SELECT a.Id, @ciment
    FROM AspNetRoles a
    JOIN @roles r ON a.NormalizedName = UPPER(r.Name)
    WHERE NOT EXISTS (SELECT 1 FROM AspNetRoles_Activités x
                      WHERE x.Role_Id = a.Id AND x.Activité_Id = @ciment);
END

-- 3) Assign users by email (only users already present in AspNetUsers) --------
-- Users are created by Admin IT in mycimar-web-client; this step is safe to re-run
-- after the missing accounts are created.
DECLARE @assign TABLE (Email NVARCHAR(256), RoleName NVARCHAR(256), Nom NVARCHAR(256), Prenom NVARCHAR(256));
INSERT INTO @assign (Email, RoleName, Nom, Prenom) VALUES
    -- Agent Logistique -> Logistique
    (N'mohamed.aggoujil@heidelbergmaterials.com',      N'Logistique',                    N'Aggoujjil',   N'Mohamed'),
    (N'safaa.elbouabidi.ext@cimar.co.ma',              N'Logistique',                    N'El Bouabidi', N'Safaa'),
    (N'adnane.elbakhri@heidelbergmaterials.com',       N'Logistique',                    N'El Bakhri',   N'Adnane'),
    (N'oussama.elhilali.ext@cimar.co.ma',              N'Logistique',                    N'El Hilali',   N'Oussama'),
    -- Agent Commercial -> Agent de Guichet
    (N'lahcen.aitlahcen@heidelbergmaterials.com',      N'Agent de Guichet',              N'Ait Lahcen',  N'Lahcen'),
    (N'hamza.eddikh@heidelbergmaterials.com',          N'Agent de Guichet',              N'Eddikh',      N'Hamza'),
    (N'said.karkouri@heidelbergmaterials.com',         N'Agent de Guichet',              N'Karkouri',    N'Said'),
    (N'oumaima.maarifa@heidelbergmaterials.com',       N'Agent de Guichet',              N'Maarifa',     N'Oumaima'),
    -- Management / ensachage
    (N'abd-ettaouab.elhrarti@heidelbergmaterials.com', N'Chef d''Agence Logistique',     N'El Hrarti',   N'Abdettaouab'),
    (N'souhail.elbasett@heidelbergmaterials.com',      N'Coordinateur Commercial',       N'Elbasett',    N'Souhail'),
    (N'mohamed.elrhattaoui@heidelbergmaterials.com',   N'Superviseur Ensachage',         N'Elrhattaoui', N'Mohamed'),
    (N'ismail.chakra@heidelbergmaterials.com',         N'Chef d''équipe Ensachage',      N'Chakra',      N'Ismail'),
    (N'abdessalam.bencheick.ext@cimar.co.ma',          N'Surveillant de quai Ensachage', N'Bencheick',   N'Abdessalam'),
    (N'abdellah.elrhattaoui@heidelbergmaterials.com',  N'Opérateur vrac Ensachage',      N'El Rhattaoui', N'Abdellah'),
    -- Platform admin (both legacy Admin and Admin IT; Ecare maps both to the Admin IT permission set)
    (N'yasser.bouaabane.ext@cimar.co.ma',              N'Admin',                         N'Bouaabane',   N'Yasser'),
    (N'yasser.bouaabane.ext@cimar.co.ma',              N'Admin IT',                      N'Bouaabane',   N'Yasser');

INSERT INTO AspNetUserRoles (UserId, RoleId)
SELECT u.Id, r.Id
FROM @assign s
JOIN AspNetUsers u ON u.NormalizedEmail = UPPER(s.Email)
JOIN AspNetRoles r ON r.NormalizedName = UPPER(s.RoleName)
WHERE NOT EXISTS (SELECT 1 FROM AspNetUserRoles ur WHERE ur.UserId = u.Id AND ur.RoleId = r.Id);

-- Report: accounts still to be created in mycimar-web-client.
SELECT s.Email, s.Nom, s.Prenom, s.RoleName AS MissingAccountForRole
FROM @assign s
WHERE NOT EXISTS (SELECT 1 FROM AspNetUsers u WHERE u.NormalizedEmail = UPPER(s.Email))
ORDER BY s.RoleName, s.Email;

-- 4) Nom / Prénom: fill only when empty (never overwrite what Admin IT typed) ---------
UPDATE u
SET u.Nom    = COALESCE(NULLIF(LTRIM(RTRIM(u.Nom)),    N''), s.Nom),
    u.Prenom = COALESCE(NULLIF(LTRIM(RTRIM(u.Prenom)), N''), s.Prenom)
FROM AspNetUsers u
JOIN @assign s ON u.NormalizedEmail = UPPER(s.Email)
WHERE NULLIF(LTRIM(RTRIM(u.Nom)), N'') IS NULL OR NULLIF(LTRIM(RTRIM(u.Prenom)), N'') IS NULL;

-- 5) Ville: all listed users belong to the Temara site -----------------------------------
-- Only fills users whose ville is empty or different; aborts loudly if Temara is not found.
DECLARE @temara INT = (SELECT TOP 1 IdVille FROM Ville WHERE NomVille LIKE N'T%mara%');

IF @temara IS NULL
    THROW 50000, N'Ville Temara introuvable dans la table Ville : vérifier NomVille.', 1;

UPDATE u
SET u.Id_Ville = @temara
FROM AspNetUsers u
JOIN @assign s ON u.NormalizedEmail = UPPER(s.Email)
WHERE u.Id_Ville IS NULL OR u.Id_Ville <> @temara;

-- 6) Verification: role, ville and activité now effective for each assigned user ------
-- Ville comes from AspNetUsers.Id_Ville (set by Admin IT at account creation).
-- Activité is derived from the role through AspNetRoles_Activités (not stored per user).
SELECT u.Email,
       u.Nom, u.Prenom,
       r.Name       AS RoleName,
       v.NomVille   AS Ville,
       act.Service  AS Activite
FROM @assign s
JOIN AspNetUsers u            ON u.NormalizedEmail = UPPER(s.Email)
JOIN AspNetUserRoles ur       ON ur.UserId = u.Id
JOIN AspNetRoles r            ON r.Id = ur.RoleId
LEFT JOIN Ville v             ON v.IdVille = u.Id_Ville
LEFT JOIN AspNetRoles_Activités ra ON ra.Role_Id = r.Id
LEFT JOIN Activite act        ON act.Id = ra.Activité_Id
ORDER BY r.Name, u.Email;
