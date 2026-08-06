-- =============================================================================
-- Mapcars — trips: rider tip (broadcast/marketplace dispatch)
-- The rider can add a tip at booking to attract drivers; it's paid on top of the
-- fare and passed 100% to the driver (no commission). Idempotent. Database-first.
-- =============================================================================

ALTER TABLE trips
    ADD COLUMN IF NOT EXISTS "TipAmount" NUMERIC(10,2) NOT NULL DEFAULT 0;
