-- 008 : file d'attente triée par heure d'affectation
--
-- QueueSnapshot.ResolveQueueTimestamp priorise désormais DateAffectation
-- (avant AddedToQueueAt/ParkingAt). La colonne existe sur Ecare_Order_Legend
-- (cf. 002_add_dateaffectation.sql) mais doit être exposée par la procédure
-- sp_GetQueueSnapshotLegend, dont la définition vit en base (non versionnée).
--
-- Ce script est une VÉRIFICATION idempotente : il n'altère rien.
-- Si la sp n'expose pas la colonne, il échoue avec l'action à faire.
-- Tant que la sp n'est pas modifiée, le code déployé reste rétro-compatible :
-- DateAffectation arrive NULL et le tri retombe sur AddedToQueueAt/ParkingAt
-- (comportement actuel inchangé).

DECLARE @def NVARCHAR(MAX) = OBJECT_DEFINITION(OBJECT_ID('dbo.sp_GetQueueSnapshotLegend'));

IF @def IS NULL
    RAISERROR('sp_GetQueueSnapshotLegend introuvable dans cette base.', 16, 1);
ELSE IF @def LIKE '%DateAffectation%'
    PRINT 'OK : sp_GetQueueSnapshotLegend expose déjà DateAffectation.';
ELSE
    RAISERROR('ACTION REQUISE : ALTER PROCEDURE dbo.sp_GetQueueSnapshotLegend — ajouter la colonne DateAffectation (Ecare_Order_Legend) à la liste SELECT retournée. Sans cela le tri par heure d''affectation reste en fallback (ordre actuel conservé).', 16, 1);
