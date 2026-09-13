-- =============================================================================
-- Mapcars — reverses 030: narrows the three CHECK constraints back to
-- ('rider','driver').
--
-- ONLY SAFE WHILE NO 'customer' ROW EXISTS. If 031 has already run, this will
-- fail on the very data 031 wrote — which is the correct behaviour, not a bug:
-- to go back past 031 you run 031_rollback.sql first, then this.
--
-- Idempotent. Database-first (no EF migrations).
--   (use the mapcars-db skill; psql is not installed on this machine)
-- =============================================================================

DO $$
DECLARE offenders bigint;
BEGIN
    SELECT count(*) INTO offenders FROM (
        SELECT 1 FROM ratings            WHERE rater_type  = 'customer'
        UNION ALL SELECT 1 FROM trip_messages      WHERE sender_type = 'customer'
        UNION ALL SELECT 1 FROM verification_codes WHERE user_type   = 'customer'
    ) s;
    IF offenders > 0 THEN
        RAISE EXCEPTION
          'Refusing to narrow: % row(s) already hold ''customer''. Run 031_rollback.sql first.',
          offenders;
    END IF;
END $$;

ALTER TABLE ratings DROP CONSTRAINT IF EXISTS "CK_ratings_rater_type";
ALTER TABLE ratings ADD  CONSTRAINT "CK_ratings_rater_type"
    CHECK (rater_type IN ('rider', 'driver'));

ALTER TABLE trip_messages DROP CONSTRAINT IF EXISTS "CK_trip_messages_sender_type";
ALTER TABLE trip_messages ADD  CONSTRAINT "CK_trip_messages_sender_type"
    CHECK (sender_type IN ('rider', 'driver'));

-- Restore the original auto-generated name, so a re-run of 003 against a fresh
-- database and a rolled-back production database end up in the same shape.
ALTER TABLE verification_codes DROP CONSTRAINT IF EXISTS "CK_verification_codes_user_type";
ALTER TABLE verification_codes DROP CONSTRAINT IF EXISTS verification_codes_user_type_check;
ALTER TABLE verification_codes ADD  CONSTRAINT verification_codes_user_type_check
    CHECK (user_type IN ('rider', 'driver'));
