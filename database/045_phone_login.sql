-- =============================================================================
-- Mapcars — 045: phone sign-in via Twilio (spec 2026-10-13 §1.2, §4.3)
-- Part 1 (this block): indexes for the per-number and platform-wide send limits.
-- Part 2 (appended in plan Task 7): normalise stored phone numbers to E.164.
-- Idempotent. Apply with or before the API image that enforces the limits.
-- =============================================================================

-- Per-number limit: every code for one phone in the last 24 h.
CREATE INDEX IF NOT EXISTS ix_verification_codes_provider_identifier_created
    ON verification_codes (provider, identifier, created_at);

-- Platform-wide cap: every phone code in the last 24 h.
CREATE INDEX IF NOT EXISTS ix_verification_codes_provider_created
    ON verification_codes (provider, created_at);
