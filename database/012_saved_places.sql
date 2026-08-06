-- =============================================================================
-- Mapcars — saved_places: a rider's saved addresses (Home/Work/custom).
-- Run AFTER 002_riders_drivers_trips.sql.
--   psql -h <aiven-host> -p <port> -U avnadmin -d defaultdb -f 012_saved_places.sql
-- =============================================================================

CREATE TABLE IF NOT EXISTS saved_places (
    "Id"           UUID             NOT NULL DEFAULT gen_random_uuid(),
    rider_id       UUID             NOT NULL,
    label          VARCHAR(40)      NOT NULL,
    address        VARCHAR(500)     NOT NULL,
    lat            DOUBLE PRECISION NOT NULL,
    lng            DOUBLE PRECISION NOT NULL,
    "CreatedAtUtc" TIMESTAMPTZ      NOT NULL DEFAULT NOW(),
    "UpdatedAtUtc" TIMESTAMPTZ,
    CONSTRAINT "PK_saved_places" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_saved_places_riders_rider_id" FOREIGN KEY (rider_id)
        REFERENCES riders ("Id") ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS "IX_saved_places_rider_id" ON saved_places (rider_id);

-- A rider can't have two places with the same label (e.g. two "Home"s).
CREATE UNIQUE INDEX IF NOT EXISTS "IX_saved_places_rider_id_label"
    ON saved_places (rider_id, lower(label));
