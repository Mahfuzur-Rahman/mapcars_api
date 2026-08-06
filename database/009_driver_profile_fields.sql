-- =============================================================================
-- Mapcars — driver profile fields: name/DOB/address/national ID/profile picture
-- Run AFTER 002_riders_drivers_trips.sql.
--   psql -h <aiven-host> -p <port> -U avnadmin -d defaultdb -f 009_driver_profile_fields.sql
-- =============================================================================

ALTER TABLE drivers
    ADD COLUMN IF NOT EXISTS "FirstName"                 VARCHAR(100),
    ADD COLUMN IF NOT EXISTS "LastName"                  VARCHAR(100),
    ADD COLUMN IF NOT EXISTS "DateOfBirth"                DATE,
    ADD COLUMN IF NOT EXISTS "Address"                    VARCHAR(500),
    ADD COLUMN IF NOT EXISTS "NationalIdNumber"           VARCHAR(50),
    ADD COLUMN IF NOT EXISTS "ProfilePictureKey"          VARCHAR(255),
    ADD COLUMN IF NOT EXISTS "ProfilePictureContentType"  VARCHAR(100);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_drivers_NationalIdNumber"
    ON drivers ("NationalIdNumber") WHERE "NationalIdNumber" IS NOT NULL;
