-- =============================================================================
-- Mapcars — closes the dual-accept window opened by 030.
--
-- WHEN: only once no image older than the 031 cutover can still be rolled back
-- to, and the 60-minute access-token tail has long passed. Two weeks is generous
-- insurance. Until then the widened constraints cost nothing.
--
-- Running this is what makes a rollback to a pre-rename image impossible, so it
-- deliberately refuses if any legacy row survives — that would mean 031 did not
-- finish, and narrowing now would reject the very rows still holding the old
-- value.
--
-- error_logs is excluded from the check on purpose: it is an append-only audit
-- trail with no CHECK constraint and no query filter, so a stray historical
-- 'rider' there is harmless and should not block the finalisation.
--
-- Idempotent. Reversible with 030_user_type_dual_accept.sql (re-widens).
-- Database-first (no EF migrations). Run AFTER 031.
--   (use the mapcars-db skill; psql is not installed on this machine)
-- =============================================================================

DO $$
DECLARE leftovers bigint;
BEGIN
    SELECT count(*) INTO leftovers FROM (
        SELECT 1 FROM ratings            WHERE rater_type  = 'rider'
        UNION ALL SELECT 1 FROM trip_messages      WHERE sender_type = 'rider'
        UNION ALL SELECT 1 FROM verification_codes WHERE user_type   = 'rider'
        UNION ALL SELECT 1 FROM device_tokens      WHERE user_type   = 'rider'
        UNION ALL SELECT 1 FROM refresh_tokens     WHERE user_type   = 'rider'
    ) s;
    IF leftovers > 0 THEN
        RAISE EXCEPTION
          'Refusing to narrow: % legacy user_type=''rider'' row(s) remain. Re-run section 5 of 031.',
          leftovers;
    END IF;
END $$;

-- Also refuse if any trip still carries the old status spelling — same reasoning
-- as above, and it is the site most easily forgotten.
DO $$
DECLARE leftovers bigint;
BEGIN
    SELECT count(*) INTO leftovers FROM trips WHERE "Status" = 'CancelledByRider';
    IF leftovers > 0 THEN
        RAISE EXCEPTION
          'Refusing to finalise: % trip(s) still hold Status=''CancelledByRider''. Re-run section 6 of 031.',
          leftovers;
    END IF;
END $$;

ALTER TABLE ratings DROP CONSTRAINT IF EXISTS "CK_ratings_rater_type";
ALTER TABLE ratings ADD  CONSTRAINT "CK_ratings_rater_type"
    CHECK (rater_type IN ('customer', 'driver'));

ALTER TABLE trip_messages DROP CONSTRAINT IF EXISTS "CK_trip_messages_sender_type";
ALTER TABLE trip_messages ADD  CONSTRAINT "CK_trip_messages_sender_type"
    CHECK (sender_type IN ('customer', 'driver'));

ALTER TABLE verification_codes DROP CONSTRAINT IF EXISTS "CK_verification_codes_user_type";
ALTER TABLE verification_codes ADD  CONSTRAINT "CK_verification_codes_user_type"
    CHECK (user_type IN ('customer', 'driver'));
