-- =============================================================================
-- Mapcars — trips: per-party chat read state (unread counts + inbox).
--
-- Unread = messages from the OTHER party sent after my read timestamp. One
-- conversation per trip, exactly two parties, so two columns on trips beat a
-- separate table. NULL = never opened (every counterpart message is unread).
--
-- Backfill: without it, every historical conversation would light up as unread
-- on the first launch of the new apps. Only rows where BOTH are NULL are touched:
-- after the API deploy, sending stamps the sender's own side, so any trip that
-- has messages and two NULLs predates this feature — re-running is safe.
--
-- trips uses all-PascalCase quoted columns (013/017/018/024/029/037/038).
-- Nullable, so safe to apply BEFORE the image that reads them.
-- Idempotent. Database-first (no EF migrations). Run AFTER 026.
--   (use the mapcars-db skill; psql is not installed on this machine)
-- =============================================================================

ALTER TABLE trips
    ADD COLUMN IF NOT EXISTS "CustomerChatReadAtUtc" TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS "DriverChatReadAtUtc"   TIMESTAMPTZ;

UPDATE trips t
   SET "CustomerChatReadAtUtc" = NOW(),
       "DriverChatReadAtUtc"   = NOW()
 WHERE t."CustomerChatReadAtUtc" IS NULL
   AND t."DriverChatReadAtUtc"   IS NULL
   AND EXISTS (SELECT 1 FROM trip_messages m WHERE m.trip_id = t."Id");
