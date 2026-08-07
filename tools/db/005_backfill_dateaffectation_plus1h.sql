-- ============================================================================
-- 005_backfill_dateaffectation_plus1h.sql  (ONE-OFF — do not automate)
-- Rows written by the old DateTime.Now code on a UTC server hold
-- "heure Maroc - 1h" in DateAffectation. This patch shifts them by +1h.
--
-- Run AFTER trigger 004 is live, and set @Cutoff to the moment the trigger
-- went live: every row stamped from then on is correct and must NOT be shifted.
--
-- If some rows BEFORE the cutoff are already correct (e.g. written by an app
-- instance where the code fix of 2026-08-04 actually worked), narrow the
-- WHERE clause after reviewing the preview — a +1h shift on a correct row
-- makes it wrong.
-- ============================================================================

DECLARE @Cutoff DATETIME = 'YYYY-MM-DDThh:mm:00';  -- <<< moment trigger 004 went live

-- ---------------------------------------------------------------------------
-- STEP 1 — PREVIEW. Review before running the update: DateAffectation should
-- normally be >= ParkingAt (a truck is affected after it parks). A negative
-- Minutes_Parking_To_Affectation is the -1h skew showing itself.
-- ---------------------------------------------------------------------------
SELECT TOP 200
       Id, Matricule, CodeSapCommande,
       ParkingAt, DateAffectation,
       DATEDIFF(MINUTE, ParkingAt, DateAffectation) AS Minutes_Parking_To_Affectation
FROM dbo.Ecare_Order_Legend
WHERE DateAffectation IS NOT NULL
  AND DateAffectation < @Cutoff
ORDER BY DateAffectation DESC;

-- ---------------------------------------------------------------------------
-- STEP 2 — PATCH. Uncomment and run once, after validating the preview.
-- The trigger ignores this update (non-NULL -> non-NULL transition).
-- ---------------------------------------------------------------------------
-- BEGIN TRANSACTION;
--
-- UPDATE dbo.Ecare_Order_Legend
-- SET DateAffectation = DATEADD(HOUR, 1, DateAffectation)
-- WHERE DateAffectation IS NOT NULL
--   AND DateAffectation < @Cutoff;
--
-- -- Check @@ROWCOUNT against the expected volume, then:
-- COMMIT TRANSACTION;   -- or ROLLBACK TRANSACTION;
