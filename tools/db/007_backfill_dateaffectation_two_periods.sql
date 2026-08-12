-- ============================================================================
-- 007_backfill_dateaffectation_two_periods.sql
-- EXECUTED against prod on 2026-08-12 via the digitalhub-mgmt admin endpoint.
-- Replaces 005 (never run — and its uniform "+1h for everything old" assumption
-- was WRONG; running it would have corrupted 2/3 of the history).
--
-- Prod data profiling (2026-08-12), using two absolute references —
-- CreatedAt (GETDATE() = UTC on Azure SQL) and PabEntryAt/StartChargingAt
-- (SQL 'AT TIME ZONE Morocco', correct) — showed:
--
--   P1  (col creation .. 2026-07-23 ~15:40 réel): DateAffectation was ALREADY
--       CORRECT (avg affectation→PAB entry = +25 min, matching the post-trigger
--       ground truth of +28; the 32 web rows = CreatedAt+1h same millisecond).
--       The original "-1h" complaint did NOT apply to this period. NO CHANGE.
--
--   P2  (2026-07-23 16:40 displayed .. 2026-08-07 03:41:25 = trigger creation):
--       BOTH apps over-shifted to Morocco+1h (30-90% of rows had the impossible
--       ordering DateAffectation > PabEntryAt; web rows showed CreatedAt+2h at
--       identical milliseconds — likely a WEBSITE_TIME_ZONE change to a UTC+2
--       zone as a fix attempt around Jul 23). FIXED with -1h (768 rows).
--
--   P3  (since 2026-08-07 03:41:25): trigger TR_..._DateAffectation_Morocco
--       stamps correct Morocco time. NO CHANGE.
--
-- Note: a +1h shift WAS first applied to P1 kiosk rows (1180) based on the 005
-- assumption, found wrong by distribution check (avg went to -33 min vs +28
-- expected), and precisely reverted the same day. Net effect on P1: none.
--
-- Statement actually applied (final net state):
UPDATE dbo.Ecare_Order_Legend
SET DateAffectation = DATEADD(HOUR, -1, DateAffectation)
WHERE DateAffectation IS NOT NULL
  AND DateAffectation >= '2026-07-23 16:40:00'   -- displayed (pre-fix) value
  AND DateAffectation < '2026-08-07 03:41:25';   -- trigger 004 creation
-- (768 rows affected on 2026-08-12)

-- Post-fix verification (ran 2026-08-12):
--   period        n     avg(DATEDIFF(MIN, DateAffectation, PabEntryAt))  aff>pab
--   P1          1212    +25 min                                          0
--   P2           767    +28 min                                          0
--   P3           534    +28 min                                          2 (legit merge-after-PAB edge cases)
