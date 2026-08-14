-- =============================================================================
-- Mapcars - refresh tokens (stay signed in until you sign out).
--
-- Until now the only credential was a 60-minute JWT with no way to renew it, so
-- every rider and driver was thrown back to the login screen once an hour -
-- including drivers mid-shift. The access token stays short (it is not revocable,
-- so a short life is the whole point); this table holds the long-lived refresh
-- token that silently mints new ones.
--
-- Design notes:
--  * token_hash stores a SHA-256 of the token, never the token itself. A leaked
--    database dump then yields nothing usable - same reason passwords are hashed.
--  * Rotation: each refresh issues a new token and marks the old one replaced.
--    Reuse of an already-rotated token means it leaked, so the whole family is
--    revoked (handled in RefreshTokenService).
--  * user_type scopes the id, because rider/driver/admin ids live in separate
--    tables and could collide.
--
-- Casing: snake_case, matching the other auth-side tables added after the base
-- schema (verification_codes, device_tokens) - see database/README.md.
--
-- Idempotent. Database-first (no EF migrations).
--   (use the mapcars-db skill; psql is not installed on this machine)
-- =============================================================================

CREATE TABLE IF NOT EXISTS refresh_tokens (
    id                     UUID         PRIMARY KEY,
    user_id                UUID         NOT NULL,
    user_type              VARCHAR(20)  NOT NULL,           -- 'rider' | 'driver' | 'admin'
    token_hash             VARCHAR(128) NOT NULL,
    expires_at_utc         TIMESTAMPTZ  NOT NULL,
    revoked_at_utc         TIMESTAMPTZ,
    replaced_by_token_hash VARCHAR(128),
    -- Free-text device label ("Pixel 8", "iPhone 15") so a future "your active
    -- sessions" screen can name them. Nullable: clients that do not send one
    -- still get a working token.
    device_label           VARCHAR(120),
    created_at_utc         TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at_utc         TIMESTAMPTZ
);

-- The lookup on every refresh call. Unique because a hash collision would let one
-- user's token resolve to another's row.
CREATE UNIQUE INDEX IF NOT EXISTS ix_refresh_tokens_hash
    ON refresh_tokens (token_hash);

-- "Revoke every session for this user" (logout-all, ban, password change).
CREATE INDEX IF NOT EXISTS ix_refresh_tokens_user
    ON refresh_tokens (user_id, user_type);

-- Cleanup sweeps of expired/revoked rows. NOW() cannot appear in an index
-- predicate (must be immutable), so index the column plainly.
CREATE INDEX IF NOT EXISTS ix_refresh_tokens_expires
    ON refresh_tokens (expires_at_utc);
