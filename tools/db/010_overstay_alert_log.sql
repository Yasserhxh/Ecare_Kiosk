-- 010 : Alerte « camion en dépassement (> 75 min dans l'usine) »
-- Journal des alertes déjà envoyées (une seule alerte par legend/visite).
-- Idempotent : réexécutable sans erreur. A exécuter AVANT le déploiement de l'API.
IF OBJECT_ID('dbo.Ecare_Overstay_Alert_Log', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Ecare_Overstay_Alert_Log
    (
        LegendId int      NOT NULL CONSTRAINT PK_Ecare_Overstay_Alert_Log PRIMARY KEY,
        SentAt   datetime NOT NULL
    );
END
GO
