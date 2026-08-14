-- =============================================================================
-- Mapcars — rider profile picture (mirrors 009_driver_profile_fields.sql).
-- Run AFTER 002_riders_drivers_trips.sql.
--   psql -h <aiven-host> -p <port> -U avnadmin -d defaultdb -f 023_rider_profile_picture.sql
-- =============================================================================

ALTER TABLE riders
    ADD COLUMN IF NOT EXISTS "ProfilePictureKey"          VARCHAR(255),
    ADD COLUMN IF NOT EXISTS "ProfilePictureContentType"  VARCHAR(100);
