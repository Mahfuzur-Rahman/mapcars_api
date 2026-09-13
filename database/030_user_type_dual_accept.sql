-- =============================================================================
-- Mapcars — user_type / rater_type / sender_type: accept 'customer' as well as
-- 'rider', ahead of the Rider → Customer rename (031).
--
-- WHY THIS IS ITS OWN SCRIPT, APPLIED DAYS EARLY.
-- Three CHECK constraints currently allow only ('rider','driver'). The moment an
-- API image that writes 'customer' goes live, every rating, chat message and OTP
-- it tries to insert would be rejected. Widening them first means the database
-- is ready before the code is — the same discipline 029 used when it gave a new
-- NOT NULL column a DB-level default so the script could land before the image.
--
-- On its own this script changes NO behaviour: the running API still only ever
-- writes 'rider'. It only makes a value legal that nothing yet produces.
--
-- The window it opens is closed again by 032, once no image older than the
-- cutover can still be rolled back to.
--
-- Idempotent: drop-then-add, so re-running is a no-op. Reversible: 030_rollback.
-- Database-first (no EF migrations). Run AFTER 029.
--   (use the mapcars-db skill; psql is not installed on this machine)
-- =============================================================================

-- ratings.rater_type — 1 'rider' row at time of writing.
-- NOTE: ratings also carries UNIQUE (trip_id, rater_type). That index is exactly
-- why 031 must rewrite this column while the API is STOPPED: if the API emitted
-- 'customer' while old rows still said 'rider', a second rating on the same trip
-- would slip past the duplicate check in RatingService and insert cleanly under
-- the different value — and 031's UPDATE would then collide with the duplicate it
-- had let through, rolling the whole migration back.
ALTER TABLE ratings DROP CONSTRAINT IF EXISTS "CK_ratings_rater_type";
ALTER TABLE ratings ADD  CONSTRAINT "CK_ratings_rater_type"
    CHECK (rater_type IN ('rider', 'customer', 'driver'));

-- trip_messages.sender_type
ALTER TABLE trip_messages DROP CONSTRAINT IF EXISTS "CK_trip_messages_sender_type";
ALTER TABLE trip_messages ADD  CONSTRAINT "CK_trip_messages_sender_type"
    CHECK (sender_type IN ('rider', 'customer', 'driver'));

-- verification_codes.user_type — the CHECK was declared INLINE in 003, so
-- Postgres auto-named it <table>_<column>_check. Confirmed live as
-- 'verification_codes_user_type_check'. Drop that and give it a real name, so
-- 032 has a deterministic handle instead of relying on a generated one.
ALTER TABLE verification_codes DROP CONSTRAINT IF EXISTS verification_codes_user_type_check;
ALTER TABLE verification_codes DROP CONSTRAINT IF EXISTS "CK_verification_codes_user_type";
ALTER TABLE verification_codes ADD  CONSTRAINT "CK_verification_codes_user_type"
    CHECK (user_type IN ('rider', 'customer', 'driver'));

-- device_tokens.user_type, refresh_tokens.user_type and error_logs.user_type
-- carry no CHECK constraint — nothing to widen there. They still hold 'rider'
-- rows that 031 rewrites; device_tokens is the one that matters most, because
-- a missed row there stops push notifications with no error anywhere.
