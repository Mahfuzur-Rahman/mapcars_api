-- =============================================================================
-- Mapcars — vehicles: the car a driver operates (one active vehicle per driver).
-- Run AFTER 002_riders_drivers_trips.sql.
--   psql -h <aiven-host> -p <port> -U avnadmin -d defaultdb -f 010_vehicles.sql
--
-- Vehicle PHOTOS are NOT stored here — they live in the documents table as
-- driver documents (DocumentType.VehicleFrontPhoto / VehicleRearPhoto /
-- VehicleInteriorPhoto). This table holds only the structured details.
-- =============================================================================

CREATE TABLE IF NOT EXISTS vehicles (
    "Id"                 UUID          NOT NULL DEFAULT gen_random_uuid(),
    driver_id            UUID          NOT NULL,
    make                 VARCHAR(60)   NOT NULL,
    model                VARCHAR(60)   NOT NULL,
    "year"               INTEGER       NOT NULL,
    colour               VARCHAR(40)   NOT NULL,
    registration_number  VARCHAR(15)   NOT NULL,
    "CreatedAtUtc"       TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    "UpdatedAtUtc"       TIMESTAMPTZ,
    CONSTRAINT "PK_vehicles" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_vehicles_drivers_driver_id" FOREIGN KEY (driver_id)
        REFERENCES drivers ("Id") ON DELETE RESTRICT
);

-- One vehicle per driver; number plates unique platform-wide.
CREATE UNIQUE INDEX IF NOT EXISTS "IX_vehicles_driver_id"           ON vehicles (driver_id);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_vehicles_registration_number" ON vehicles (registration_number);
