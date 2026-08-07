-- ============================================================================
-- 004_trg_dateaffectation_morocco_time.sql
-- Makes the DATABASE the source of truth for DateAffectation's wall clock.
-- Whenever DateAffectation transitions NULL -> value, the trigger re-stamps it
-- with SYSDATETIMEOFFSET() AT TIME ZONE 'Morocco Standard Time' — the same
-- convention as ParkingAt / PabEntryAt / CreatedAt — so an app server running
-- in UTC can no longer write "heure Maroc - 1h", whatever its timezone config.
--
-- !! DEPLOY ORDER — IMPORTANT !!
-- EF Core 7+ uses the OUTPUT clause on SaveChanges, and SQL Server rejects
-- OUTPUT on a table that has ANY enabled trigger.
--   - mycimar-web-client (EF Core 8) MUST be deployed with the .HasTrigger(...)
--     mapping in AppDbContext (EcareOrderLegend) BEFORE this script is run,
--     otherwise AssignTransporteurToOrder breaks at runtime.
--   - mycimar-web-api (EF Core 6) is safe as-is: EF 6 does not use OUTPUT.
--     If it is ever upgraded to EF 7+, add HasTrigger there too.
--   - The kiosk API (Dapper, raw SQL) is not affected by the trigger.
--
-- Guards:
--   - write-once preserved: fires only on NULL -> value transitions;
--   - only "live" writes are re-stamped (value within 2h of Morocco now), so a
--     sync or backfill carrying a historical DateAffectation passes through;
--   - recursion-safe: on re-entry the NULL -> value guard no longer matches.
-- Safe to run multiple times (CREATE OR ALTER).
-- ============================================================================

CREATE OR ALTER TRIGGER dbo.TR_Ecare_Order_Legend_DateAffectation_Morocco
ON dbo.Ecare_Order_Legend
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT UPDATE(DateAffectation)
        RETURN;

    DECLARE @moroccoNow DATETIME =
        CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'Morocco Standard Time' AS DATETIME);

    UPDATE l
    SET l.DateAffectation = @moroccoNow
    FROM dbo.Ecare_Order_Legend AS l
    INNER JOIN inserted AS i ON i.Id = l.Id
    LEFT JOIN deleted  AS d ON d.Id = i.Id
    WHERE i.DateAffectation IS NOT NULL
      AND (d.Id IS NULL OR d.DateAffectation IS NULL)                    -- NULL -> value only
      AND ABS(DATEDIFF(MINUTE, i.DateAffectation, @moroccoNow)) <= 120;  -- live writes only
END;
GO
