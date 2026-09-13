-- =============================================================================
-- Mapcars — trips: search window (request expiry).
-- An open request had no deadline: the dispatch ring widened to its maximum at
-- two minutes and then stopped, leaving the trip 'Requested' until the rider
-- cancelled by hand. This gives every request a 3-minute window the rider can
-- extend twice, after which the trip closes as 'Expired' (a new TripStatus).
--   "ExpiresAtUtc"   — end of the CURRENT window; the server owns it and both
--                      apps count down to it. Past it, the request is paused:
--                      off every driver board and unacceptable, but still
--                      extendable for one minute of grace.
--   "ExtensionCount" — 0..2, bounding how long a trip sits holding a fare that
--                      was priced at booking.
-- Backfill: existing rows get CreatedAtUtc + 3min, already long past, so they
-- are inert. NOTE any trip still sitting 'Requested' from before this change is
-- swept to 'Expired' shortly after the API starts. That is the intended
-- cleanup of the hung requests this feature exists to prevent — expect it.
-- Idempotent. Database-first (no EF migrations). Run AFTER 002/013/024.
--   (use the mapcars-db skill; psql is not installed on this machine)
-- =============================================================================

-- trips uses all-PascalCase quoted columns even for later additions (see 013/017/018/024).
ALTER TABLE trips
    ADD COLUMN IF NOT EXISTS "ExpiresAtUtc"   TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS "ExtensionCount" INTEGER NOT NULL DEFAULT 0;

UPDATE trips
   SET "ExpiresAtUtc" = "CreatedAtUtc" + INTERVAL '3 minutes'
 WHERE "ExpiresAtUtc" IS NULL;

-- The DEFAULT is what makes this script safe to run BEFORE the new API image is
-- deployed, and that ordering is not optional: the running API knows nothing
-- about this column, so a bare NOT NULL would reject its every INSERT and stop
-- riders booking the moment this script ran.
--
-- It costs a little strictness — an application bug that forgot to set the
-- deadline would get a silent 3-minute window instead of a loud failure — but
-- the API sets it explicitly in TripService.CreateAsync, and a window of
-- exactly the right length is a mild failure mode next to a dead booking flow.
ALTER TABLE trips
    ALTER COLUMN "ExpiresAtUtc" SET DEFAULT (NOW() + INTERVAL '3 minutes');

ALTER TABLE trips
    ALTER COLUMN "ExpiresAtUtc" SET NOT NULL;

-- The board queries and the lifecycle sweeper both ask the same question:
-- "open requests whose window is still (or no longer) live". Status leads the
-- index because it is the selective half — open trips are a sliver of the table.
CREATE INDEX IF NOT EXISTS ix_trips_status_expires
    ON trips ("Status", "ExpiresAtUtc");
