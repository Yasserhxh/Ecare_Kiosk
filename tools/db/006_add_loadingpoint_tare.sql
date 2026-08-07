-- ============================================================================
-- 006_add_loadingpoint_tare.sql
-- Tare relevée par la bascule du point de chargement VRAC (Vr1/Vr2/Vr3), en kg,
-- au moment où le camion vide est positionné sur la ligne. Valeur de comparaison
-- avec la tare légale PremierePoid (bascule IN / PAB). Write-once côté app.
-- IMPORTANT: run on the Ecare database BEFORE deploying the kiosk API build.
-- Safe to run multiple times (guarded by COL_LENGTH).
-- ============================================================================

IF COL_LENGTH('dbo.Ecare_Order_Legend', 'LoadingPointTare') IS NULL
BEGIN
    ALTER TABLE dbo.Ecare_Order_Legend
        ADD LoadingPointTare INT NULL;
END;
GO
