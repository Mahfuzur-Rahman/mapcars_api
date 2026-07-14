-- =============================================================================
-- Mapcars — documents: rider identity/KYC docs + driver licensing/vehicle docs
-- Run AFTER 002_riders_drivers_trips.sql.
--   psql -h <aiven-host> -p <port> -U avnadmin -d defaultdb -f 005_documents.sql
--
-- Exactly one of rider_id/driver_id is set per row — enforced by the CHECK
-- constraint below, mirroring how trips."DriverId" is nullable.
-- =============================================================================

CREATE TABLE IF NOT EXISTS documents (
    "Id"               UUID             NOT NULL DEFAULT gen_random_uuid(),
    rider_id           UUID,
    driver_id          UUID,
    "Type"             VARCHAR(30)      NOT NULL,
    storage_key        VARCHAR(260)     NOT NULL,
    original_file_name VARCHAR(260)     NOT NULL,
    content_type       VARCHAR(100)     NOT NULL,
    review_status      VARCHAR(20)      NOT NULL DEFAULT 'Pending',
    reviewed_at_utc    TIMESTAMPTZ,
    "CreatedAtUtc"     TIMESTAMPTZ      NOT NULL DEFAULT NOW(),
    "UpdatedAtUtc"     TIMESTAMPTZ,
    CONSTRAINT "PK_documents" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_documents_riders_rider_id"   FOREIGN KEY (rider_id)
        REFERENCES riders ("Id")  ON DELETE RESTRICT,
    CONSTRAINT "FK_documents_drivers_driver_id" FOREIGN KEY (driver_id)
        REFERENCES drivers ("Id") ON DELETE RESTRICT,
    CONSTRAINT "CK_documents_exactly_one_owner" CHECK (
        (rider_id IS NOT NULL AND driver_id IS NULL) OR
        (rider_id IS NULL AND driver_id IS NOT NULL)
    )
);

CREATE INDEX IF NOT EXISTS "IX_documents_rider_id"  ON documents (rider_id);
CREATE INDEX IF NOT EXISTS "IX_documents_driver_id" ON documents (driver_id);
