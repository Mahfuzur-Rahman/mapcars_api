-- =============================================================================
-- Mapcars — ratings: rider<->driver 1-5 star review per trip, one per
-- direction. Plus average-rating/rating-count aggregates on riders & drivers.
-- Run AFTER 002_riders_drivers_trips.sql.
--   psql -h <aiven-host> -p <port> -U avnadmin -d defaultdb -f 014_ratings.sql
-- =============================================================================

CREATE TABLE IF NOT EXISTS ratings (
    "Id"           UUID         NOT NULL DEFAULT gen_random_uuid(),
    trip_id        UUID         NOT NULL,
    rater_type     VARCHAR(10)  NOT NULL,   -- 'rider' | 'driver' (who submitted the rating)
    score          INTEGER      NOT NULL,
    comment        VARCHAR(1000),
    "CreatedAtUtc" TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    "UpdatedAtUtc" TIMESTAMPTZ,
    CONSTRAINT "PK_ratings" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ratings_trips_trip_id" FOREIGN KEY (trip_id)
        REFERENCES trips ("Id") ON DELETE RESTRICT,
    CONSTRAINT "CK_ratings_rater_type" CHECK (rater_type IN ('rider', 'driver'))
);

-- One rating per direction per trip (a rider can't rate the same trip twice).
CREATE UNIQUE INDEX IF NOT EXISTS "IX_ratings_trip_id_rater_type" ON ratings (trip_id, rater_type);

ALTER TABLE riders
    ADD COLUMN IF NOT EXISTS average_rating NUMERIC(3,2),
    ADD COLUMN IF NOT EXISTS rating_count   INTEGER NOT NULL DEFAULT 0;

ALTER TABLE drivers
    ADD COLUMN IF NOT EXISTS average_rating NUMERIC(3,2),
    ADD COLUMN IF NOT EXISTS rating_count   INTEGER NOT NULL DEFAULT 0;
