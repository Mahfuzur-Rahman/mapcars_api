-- =============================================================================
-- Mapcars — push-notification device tokens.
-- One row per app install (FCM registration token), owned by a rider or driver.
-- Keyed on the token (upserted) since a token is unique per install and can move
-- between users on a shared device. Idempotent. Database-first (no EF migrations).
--   (use the mapcars-db skill; psql is not installed on this machine)
-- =============================================================================

CREATE TABLE IF NOT EXISTS device_tokens (
    id             UUID         NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    user_type      VARCHAR(10)  NOT NULL,          -- 'rider' | 'driver'
    user_id        UUID         NOT NULL,
    token          VARCHAR(512) NOT NULL,          -- FCM registration token
    platform       VARCHAR(10),                    -- 'android' | 'ios'
    created_at_utc TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at_utc TIMESTAMPTZ
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_device_tokens_token ON device_tokens (token);
CREATE INDEX IF NOT EXISTS ix_device_tokens_owner ON device_tokens (user_type, user_id);
