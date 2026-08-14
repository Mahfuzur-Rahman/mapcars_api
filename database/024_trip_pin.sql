-- =============================================================================
-- Mapcars — trips: meet-up PIN.
-- A 4-digit code generated at booking. The rider sees it on their tracking
-- screen; the driver confirms it on the arrived screen before starting the
-- trip, which is what proves they picked up the right person. Until now both
-- apps showed a hard-coded '4821' with nothing behind it.
-- Nullable: trips booked before this column existed have no PIN, and the
-- driver app treats a null PIN as "nothing to confirm" rather than blocking.
-- Idempotent. Database-first (no EF migrations). Run AFTER 002/013.
--   (use the mapcars-db skill; psql is not installed on this machine)
-- =============================================================================

-- trips uses all-PascalCase quoted columns even for later additions (see 013/017/018).
ALTER TABLE trips
    ADD COLUMN IF NOT EXISTS "Pin" VARCHAR(4);
