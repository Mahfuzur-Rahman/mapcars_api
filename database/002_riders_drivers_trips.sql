-- =============================================================================
-- Mapcars — Core domain schema: riders, drivers, trips
-- Database-first: this file is the source of truth for these tables.
-- There are NO EF Core migrations — edit this script to evolve the schema.
--   psql -U postgres -d mapcars -f 002_riders_drivers_trips.sql
--
-- Column casing note: base columns use "PascalCase" (EF default convention)
-- and auth columns use snake_case. The EF configurations in
-- Mapcars.Infrastructure/Persistence/Configurations map to these exact names —
-- keep them in sync.
-- =============================================================================

-- ─── riders ──────────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS riders (
    "Id"                UUID             NOT NULL DEFAULT gen_random_uuid(),
    "FullName"          VARCHAR(200),
    "Email"             VARCHAR(256),
    "PhoneNumber"       VARCHAR(20),
    password_hash       VARCHAR(255),
    google_sub          VARCHAR(255),
    is_email_verified   BOOLEAN          NOT NULL DEFAULT FALSE,
    is_phone_verified   BOOLEAN          NOT NULL DEFAULT FALSE,
    "IsActive"          BOOLEAN          NOT NULL DEFAULT TRUE,
    "CreatedAtUtc"      TIMESTAMPTZ      NOT NULL DEFAULT NOW(),
    "UpdatedAtUtc"      TIMESTAMPTZ,
    CONSTRAINT "PK_riders" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_riders_Email"
    ON riders ("Email") WHERE "Email" IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS uix_riders_phone
    ON riders ("PhoneNumber") WHERE "PhoneNumber" IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS uix_riders_google_sub
    ON riders (google_sub) WHERE google_sub IS NOT NULL;

-- ─── drivers ─────────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS drivers (
    "Id"                UUID             NOT NULL DEFAULT gen_random_uuid(),
    "FullName"          VARCHAR(200),
    "Email"             VARCHAR(256),
    "PhoneNumber"       VARCHAR(20),
    "PhvLicenceNumber"  VARCHAR(50),
    password_hash       VARCHAR(255),
    google_sub          VARCHAR(255),
    is_email_verified   BOOLEAN          NOT NULL DEFAULT FALSE,
    is_phone_verified   BOOLEAN          NOT NULL DEFAULT FALSE,
    "Status"            VARCHAR(30)      NOT NULL DEFAULT 'PendingApproval',
    "CreatedAtUtc"      TIMESTAMPTZ      NOT NULL DEFAULT NOW(),
    "UpdatedAtUtc"      TIMESTAMPTZ,
    CONSTRAINT "PK_drivers" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_drivers_Email"
    ON drivers ("Email") WHERE "Email" IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS "IX_drivers_PhvLicenceNumber"
    ON drivers ("PhvLicenceNumber") WHERE "PhvLicenceNumber" IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS uix_drivers_phone
    ON drivers ("PhoneNumber") WHERE "PhoneNumber" IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS uix_drivers_google_sub
    ON drivers (google_sub) WHERE google_sub IS NOT NULL;

-- ─── trips ───────────────────────────────────────────────────────────────────
-- Status is stored as text (the TripStatus enum name), per EF HasConversion<string>().

CREATE TABLE IF NOT EXISTS trips (
    "Id"             UUID              NOT NULL DEFAULT gen_random_uuid(),
    "RiderId"        UUID              NOT NULL,
    "DriverId"       UUID,
    "PickupAddress"  VARCHAR(500)      NOT NULL,
    "PickupLat"      DOUBLE PRECISION  NOT NULL,
    "PickupLng"      DOUBLE PRECISION  NOT NULL,
    "DropoffAddress" VARCHAR(500)      NOT NULL,
    "DropoffLat"     DOUBLE PRECISION  NOT NULL,
    "DropoffLng"     DOUBLE PRECISION  NOT NULL,
    "Status"         VARCHAR(30)       NOT NULL DEFAULT 'Requested',
    "FareAmount"     NUMERIC(10,2),
    "CreatedAtUtc"   TIMESTAMPTZ       NOT NULL DEFAULT NOW(),
    "UpdatedAtUtc"   TIMESTAMPTZ,
    CONSTRAINT "PK_trips" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_trips_riders_RiderId"  FOREIGN KEY ("RiderId")
        REFERENCES riders ("Id")  ON DELETE RESTRICT,
    CONSTRAINT "FK_trips_drivers_DriverId" FOREIGN KEY ("DriverId")
        REFERENCES drivers ("Id") ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS "IX_trips_RiderId"  ON trips ("RiderId");
CREATE INDEX IF NOT EXISTS "IX_trips_DriverId" ON trips ("DriverId");
CREATE INDEX IF NOT EXISTS "IX_trips_Status"   ON trips ("Status");
