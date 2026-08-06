-- =============================================================================
-- Mapcars — trips: rider payment method + settlement state.
-- Step 1 (cash / no-charge): a trip is booked with a payment method (default
-- 'Cash') and a settlement status. Cash trips go Pending → Collected when the
-- driver completes the trip — no money moves through the platform, so the whole
-- ride loop is testable without a real charge. Card charging (Stripe) comes next
-- and reuses these columns (adds authorize/capture transitions).
-- Idempotent. Database-first (no EF migrations). Run AFTER 002/013.
--   (use the mapcars-db skill; psql is not installed on this machine)
-- =============================================================================

-- trips uses all-PascalCase quoted columns even for later additions (see 013/017).
ALTER TABLE trips
    ADD COLUMN IF NOT EXISTS "PaymentMethod" VARCHAR(20) NOT NULL DEFAULT 'Cash',
    ADD COLUMN IF NOT EXISTS "PaymentStatus" VARCHAR(20) NOT NULL DEFAULT 'Pending',
    ADD COLUMN IF NOT EXISTS "PaidAtUtc"     TIMESTAMPTZ;
