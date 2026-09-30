-- 011 : sp_GetQueueSnapshotLegend — exposer DateAffectation
-- Exécuté en prod le 01/10/2026 via l'endpoint admin SQL.
-- Seule modification vs la version précédente (récupérée de la base, jamais
-- versionnée jusqu'ici) : ajout de L.DateAffectation au SELECT final.
-- Active le tri de la file d'attente par heure d'affectation (cf. 008 pour la
-- vérification ; QueueSnapshot.ResolveQueueTimestamp côté code).

ALTER PROCEDURE [dbo].[sp_GetQueueSnapshotLegend]
AS
BEGIN
    SET NOCOUNT ON;

    /* Une ligne par Matricule dans Equipements (la plus récente par Id) */
    WITH SingleEquip AS (
        SELECT *
        FROM (
            SELECT *,
                   ROW_NUMBER() OVER (PARTITION BY Matricule ORDER BY Id DESC) AS rn
            FROM dbo.Ecare_ClientEquipements
        ) t
        WHERE rn = 1
    ),
    /* Une ligne par Matricule dans Legend (Step = 1) */
    SingleLegend AS (
        SELECT *
        FROM (
            SELECT *,
                   ROW_NUMBER() OVER (PARTITION BY Matricule ORDER BY AddedToQueueAt ASC) AS rn
            FROM dbo.Ecare_Order_Legend
            WHERE Step = 1
              AND AnnulationCommercial IS NULL
        ) x
        WHERE rn = 1
    )
    SELECT
        L.Id,
        L.OrderId,
        L.ClientName,
        L.Chantier,
        L.Matricule,
        L.RFIDCard,
        L.TypeCamion,
        L.NombrePlombs,
        L.Produit1,
        L.Quantite1,
        L.IsPined,
        L.PinedAt,
        L.AddedToQueueAt,
        L.FirstPlaceAt,
        L.TimeElapsedInFirstPlace,
        L.Step,
        T.TruckType,
        T.ChauffeurName,       -- depuis Ecare_ClientEquipements (plus récent)
        L.ParkingAt,
        L.DateAffectation,     -- 011 : clé de tri prioritaire de la file d'attente
        L.TypeProduit
    FROM SingleLegend L
    LEFT JOIN SingleEquip T
        ON T.Matricule = L.Matricule
    ORDER BY L.AddedToQueueAt ASC;
END
GO
