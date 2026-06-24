-- ============================================================================
-- 002_add_dateaffectation.sql
-- Adds the write-once "date d'affectation" column to Ecare_Order_Legend.
-- This is the moment a commande is affected to a matricule (the parking->SAP merge),
-- moving the truck from "en validation" into the main file d'attente.
--
-- IMPORTANT: run this on the Ecare database BEFORE deploying the new build.
-- The merge handler (MergeParkingWithSapHandler) and the legend queries reference
-- this column; without it they will fail at runtime.
-- Safe to run multiple times (guarded by COL_LENGTH).
-- ============================================================================

IF COL_LENGTH('dbo.Ecare_Order_Legend', 'DateAffectation') IS NULL
BEGIN
    ALTER TABLE dbo.Ecare_Order_Legend
        ADD DateAffectation DATETIME NULL;
END;
GO
