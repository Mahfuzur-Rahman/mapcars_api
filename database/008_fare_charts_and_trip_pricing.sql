-- =============================================================================
-- Mapcars — Pricing: fare chart history + trip fare breakdown
-- Database-first: this file is the source of truth for these tables/columns.
-- There are NO EF Core migrations — edit this script to evolve the schema.
--   psql -U postgres -d mapcars -f 008_fare_charts_and_trip_pricing.sql
--
-- Redis is the hot cache for the *current* fare chart; `fare_charts` below is the
-- durable version history (source of truth) so the chart survives a Redis flush.
-- Column casing note: base columns use "PascalCase" (EF default convention).
-- =============================================================================

-- ─── fare_charts ─────────────────────────────────────────────────────────────
-- One row per published fare chart. The row with the highest "Version" is current.
-- The structured chart is stored whole as JSONB (never queried column-wise).

CREATE TABLE IF NOT EXISTS fare_charts (
    "Id"            UUID         NOT NULL DEFAULT gen_random_uuid(),
    "Version"       INTEGER      NOT NULL,
    "PayloadJson"   JSONB        NOT NULL,
    "CreatedAtUtc"  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    "UpdatedAtUtc"  TIMESTAMPTZ,
    CONSTRAINT "PK_fare_charts" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_fare_charts_Version"
    ON fare_charts ("Version");

-- ─── trips: fare breakdown snapshot ──────────────────────────────────────────
-- Captured at booking so a trip's fare is auditable and immune to later chart
-- edits. Money in GBP NUMERIC(10,2); distance in miles; surge as a multiplier.

ALTER TABLE trips
    ADD COLUMN IF NOT EXISTS "Tier"              VARCHAR(30),
    ADD COLUMN IF NOT EXISTS "DistanceMiles"     DOUBLE PRECISION,
    ADD COLUMN IF NOT EXISTS "DurationMinutes"   DOUBLE PRECISION,
    ADD COLUMN IF NOT EXISTS "SurgeMultiplier"   NUMERIC(6,3),
    ADD COLUMN IF NOT EXISTS "PlatformFeeAmount" NUMERIC(10,2),
    ADD COLUMN IF NOT EXISTS "DriverEarnings"    NUMERIC(10,2),
    ADD COLUMN IF NOT EXISTS "FareChartVersion"  INTEGER;
