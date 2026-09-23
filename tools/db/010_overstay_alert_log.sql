-- 010 : Alerte « camion en dépassement dans l'usine »
-- Journal des alertes envoyées : une ligne par (camion, palier) — paliers 75 et 90 min.
-- Idempotent : réexécutable sans erreur. A exécuter AVANT le déploiement de l'API.
IF OBJECT_ID('dbo.Ecare_Overstay_Alert_Log', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Ecare_Overstay_Alert_Log
    (
        LegendId         int      NOT NULL,
        ThresholdMinutes int      NOT NULL,   -- palier alerté (ex. 75, 90)
        SentAt           datetime NOT NULL,
        CONSTRAINT PK_Ecare_Overstay_Alert_Log PRIMARY KEY (LegendId, ThresholdMinutes)
    );
END
GO
