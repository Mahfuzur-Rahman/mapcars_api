-- =============================================================================
-- Mapcars — verification_codes: add sent_via column + cleanup index
-- Run AFTER 003_verification_codes.sql (idempotent — safe to re-run).
--   psql -h <aiven-host> -p <port> -U avnadmin -d defaultdb -f 004_verification_codes_sent_via.sql
--
-- What this adds:
--   1. sent_via  — which provider delivered the OTP (twilio | telnyx | smtp |
--                  resend | console).  NULL on rows created before this script.
--   2. idx_vc_expires_at — lets a nightly cleanup job efficiently DELETE rows
--                          WHERE used_at IS NOT NULL OR expires_at < NOW() - '30 days'
-- =============================================================================

-- 1. Add sent_via (nullable so existing rows are unaffected)
ALTER TABLE verification_codes
    ADD COLUMN IF NOT EXISTS sent_via VARCHAR(20);

-- 2. Index for bulk-deleting expired / used codes (cheap maintenance job).
--    No NOW() in the predicate — index predicates must be immutable.
CREATE INDEX IF NOT EXISTS idx_vc_expires_at
    ON verification_codes (expires_at);

-- Optional cleanup — remove codes older than 30 days (run manually or via pg_cron)
-- DELETE FROM verification_codes
-- WHERE expires_at < NOW() - INTERVAL '30 days';
