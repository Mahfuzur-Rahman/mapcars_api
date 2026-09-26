-- =============================================================================
-- Mapcars — customers + drivers: Sign in with Apple.
--
-- App Review guideline 4.8: an iOS app that offers a third-party sign-in (we
-- offer Google) must also offer Sign in with Apple. apple_sub is Apple's stable,
-- app-scoped user id — the identity, exactly as google_sub is for Google. Apple
-- only reveals the email on the FIRST authorisation (and may give a private
-- relay address), so the email can never be the key.
--
-- Unique where present, mirroring uix_*_google_sub: one Apple ID, one account.
--
-- Additive, so safe to apply BEFORE the image that uses it (the 029 discipline).
-- The new image REQUIRES it — EF maps apple_sub on both entities, so every
-- customer and driver query fails until this has run. Apply first.
--
-- Idempotent. Database-first (no EF migrations). Run AFTER 031.
--   (use the mapcars-db skill; psql is not installed on this machine)
-- =============================================================================

ALTER TABLE customers ADD COLUMN IF NOT EXISTS apple_sub VARCHAR(255);
ALTER TABLE drivers   ADD COLUMN IF NOT EXISTS apple_sub VARCHAR(255);

CREATE UNIQUE INDEX IF NOT EXISTS uix_customers_apple_sub
    ON customers (apple_sub) WHERE apple_sub IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS uix_drivers_apple_sub
    ON drivers (apple_sub) WHERE apple_sub IS NOT NULL;
