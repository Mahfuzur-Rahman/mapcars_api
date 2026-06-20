-- =============================================================================
-- Mapcars — Verification codes (OTP for email + phone, riders & drivers)
-- Database-first: source of truth for the verification_codes table.
--   psql -U postgres -d mapcars -f 003_verification_codes.sql
-- =============================================================================

CREATE TABLE IF NOT EXISTS verification_codes (
    id          UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    user_type   VARCHAR(10)  NOT NULL CHECK (user_type IN ('rider', 'driver')),
    provider    VARCHAR(10)  NOT NULL CHECK (provider IN ('email', 'phone')),
    identifier  VARCHAR(255) NOT NULL,
    code        CHAR(6)      NOT NULL,
    expires_at  TIMESTAMPTZ  NOT NULL,
    used_at     TIMESTAMPTZ,
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

-- Fast lookup: find the latest unused code for an identifier.
CREATE INDEX IF NOT EXISTS idx_vc_lookup
    ON verification_codes (provider, identifier, expires_at)
    WHERE used_at IS NULL;
