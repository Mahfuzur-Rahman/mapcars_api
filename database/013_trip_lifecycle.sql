-- =============================================================================
-- Mapcars — trip lifecycle: completion/cancellation timestamps + reason/no-show
-- on trips; cancellation/no-show counters on riders & drivers; driver
-- online/offline availability toggle.
-- Run AFTER 002_riders_drivers_trips.sql.
--   psql -h <aiven-host> -p <port> -U avnadmin -d defaultdb -f 013_trip_lifecycle.sql
-- =============================================================================

-- trips already uses all-PascalCase columns even for later additions
-- (see 008_fare_charts_and_trip_pricing.sql) — match that convention here.
ALTER TABLE trips
    ADD COLUMN IF NOT EXISTS "CompletedAtUtc"  TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS "CancelledAtUtc"  TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS "CancelledReason" VARCHAR(500),
    ADD COLUMN IF NOT EXISTS "IsNoShow"        BOOLEAN NOT NULL DEFAULT FALSE;

-- riders/drivers use snake_case for later-added columns (Wave 1 convention).
ALTER TABLE riders
    ADD COLUMN IF NOT EXISTS cancellation_count INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS no_show_count      INTEGER NOT NULL DEFAULT 0;

ALTER TABLE drivers
    ADD COLUMN IF NOT EXISTS cancellation_count  INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS no_show_count        INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS is_online            BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS last_online_at_utc    TIMESTAMPTZ;
