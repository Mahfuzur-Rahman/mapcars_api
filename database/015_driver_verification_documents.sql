-- =============================================================================
-- Mapcars — driver verification documents: passport number field, plus new
-- DocumentType values (Passport, DrivingLicence, VehicleBadge, BankStatement)
-- and ProofOfAddress now also accepted from drivers (utility bill). The
-- DocumentType enum values live in application code (Document."Type" is a
-- free-text VARCHAR) — no schema change needed for the new types themselves.
-- Run AFTER 002_riders_drivers_trips.sql.
--   psql -h <aiven-host> -p <port> -U avnadmin -d defaultdb -f 015_driver_verification_documents.sql
-- =============================================================================

ALTER TABLE drivers
    ADD COLUMN IF NOT EXISTS passport_number VARCHAR(50);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_drivers_passport_number"
    ON drivers (passport_number) WHERE passport_number IS NOT NULL;
