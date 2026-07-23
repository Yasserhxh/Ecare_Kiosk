-- ============================================================================
-- 003_add_pab_deviceid.sql
-- Adds traceability columns to Ecare_Order_Legend: which bascule (deviceId)
-- captured the first weight (PabEntreeDeviceId) and the second weight
-- (PabSortieDeviceId). Incident 2026-07-22: a second weight was written from
-- the wrong bridge session; these columns let us trace which PAB did what.
--
-- IMPORTANT: run this on the Ecare database BEFORE deploying the new build.
-- UpdateAfterFirstWeightHandler / UpdateSecondWeightHandler and the
-- legend-business query reference these columns.
-- Safe to run multiple times (guarded by COL_LENGTH).
-- ============================================================================

IF COL_LENGTH('dbo.Ecare_Order_Legend', 'PabEntreeDeviceId') IS NULL
BEGIN
    ALTER TABLE dbo.Ecare_Order_Legend
        ADD PabEntreeDeviceId NVARCHAR(50) NULL;
END;
GO

IF COL_LENGTH('dbo.Ecare_Order_Legend', 'PabSortieDeviceId') IS NULL
BEGIN
    ALTER TABLE dbo.Ecare_Order_Legend
        ADD PabSortieDeviceId NVARCHAR(50) NULL;
END;
GO
