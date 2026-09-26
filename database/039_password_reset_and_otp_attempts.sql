-- =============================================================================
-- Mapcars — verification_codes: password-reset codes, and a cap on guesses.
--
-- WHAT THIS ADDS.
--   * provider 'reset' — a self-service password reset code, emailed. Kept as its
--     own provider (not 'email') so that asking for a reset can never burn the
--     sign-up verification code that is in flight for the same address, and a
--     sign-up code can never be replayed as a reset. VARCHAR(10), hence 'reset'.
--   * attempt_count — wrong guesses against the current code. Until now a code
--     could be guessed without limit for its whole lifetime; the only brake was
--     the per-IP rate limit, and rotating IPs is trivial. 6 digits is 900,000
--     codes; a reset code is a key to the whole account. The service burns a
--     code after 5 wrong guesses, whatever IP they come from.
--
-- Additive and DEFAULTed, so safe to apply BEFORE the image that uses it (the
-- 029 discipline): the old image never selects attempt_count and never writes
-- 'reset'. The new image REQUIRES it — EF maps attempt_count, so every OTP
-- query would fail until this has run. Apply first.
--
-- Idempotent. Database-first (no EF migrations). Run AFTER 004.
--   (use the mapcars-db skill; psql is not installed on this machine)
-- =============================================================================

ALTER TABLE verification_codes
    ADD COLUMN IF NOT EXISTS attempt_count INTEGER NOT NULL DEFAULT 0;

-- Widen the provider CHECK. Guarded: only replace it if it doesn't already
-- accept 'reset', so a re-run is a no-op.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conrelid = 'verification_codes'::regclass
          AND conname = 'verification_codes_provider_check'
          AND pg_get_constraintdef(oid) LIKE '%reset%'
    ) THEN
        ALTER TABLE verification_codes DROP CONSTRAINT IF EXISTS verification_codes_provider_check;
        ALTER TABLE verification_codes
            ADD CONSTRAINT verification_codes_provider_check
            CHECK (provider IN ('email', 'phone', 'reset'));
    END IF;
END $$;
