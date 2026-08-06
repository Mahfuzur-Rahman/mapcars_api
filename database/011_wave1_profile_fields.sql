-- =============================================================================
-- Mapcars — Wave 1 profile/compliance fields: emergency contact, marketing
-- consent, accessibility needs (riders), DVLA driving licence number
-- (drivers), PHV vehicle licence/council (vehicles), document expiry dates.
-- Run AFTER 002_riders_drivers_trips.sql / 005_documents.sql / 010_vehicles.sql.
--   psql -h <aiven-host> -p <port> -U avnadmin -d defaultdb -f 011_wave1_profile_fields.sql
-- =============================================================================

ALTER TABLE riders
    ADD COLUMN IF NOT EXISTS emergency_contact_name  VARCHAR(200),
    ADD COLUMN IF NOT EXISTS emergency_contact_phone VARCHAR(20),
    ADD COLUMN IF NOT EXISTS marketing_consent       BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS accessibility_needs     VARCHAR(500);

ALTER TABLE drivers
    ADD COLUMN IF NOT EXISTS driving_licence_number  VARCHAR(20),
    ADD COLUMN IF NOT EXISTS emergency_contact_name   VARCHAR(200),
    ADD COLUMN IF NOT EXISTS emergency_contact_phone  VARCHAR(20),
    ADD COLUMN IF NOT EXISTS marketing_consent        BOOLEAN NOT NULL DEFAULT FALSE;

ALTER TABLE vehicles
    ADD COLUMN IF NOT EXISTS phv_licence_plate_number VARCHAR(30),
    ADD COLUMN IF NOT EXISTS phv_licensing_authority  VARCHAR(120);

ALTER TABLE documents
    ADD COLUMN IF NOT EXISTS expires_on DATE;
