-- =============================================================================
-- Mapcars — trips: record that the kerbside PIN was actually checked.
--
-- WHAT WAS WRONG. 024 added a 4-digit meet-up PIN, but it was only ever compared
-- inside the driver app. The server took no PIN, checked nothing, and kept no
-- record — so a driver calling POST /trips/{id}/start directly skipped it
-- entirely, and for any trip that was later disputed there was no evidence it
-- had happened at all. A client-side check is a UX affordance, not a control.
--
-- WHY IT MATTERS FOR MONEY, not just tidiness:
--   * Anti-collusion. The highest-value attack on a two-sided platform is a
--     driver manufacturing trips for customer accounts they also control. A
--     manufactured trip has no passenger at the kerb to read a code out, so a
--     population of completed trips with PinVerifiedAtUtc always null is a
--     signal worth having.
--   * Dispute evidence. "Somebody at the pickup point knew a number only the
--     booker was shown, at 14:02" is the strongest single fact available in a
--     chargeback, and it is currently not recorded anywhere.
--
-- PinAttemptCount exists because 4 digits is 10,000 guesses, which is nothing
-- over an API. Without a cap a driver could brute-force their way to a
-- "verified" pickup that never happened.
--
-- Both columns are nullable or DEFAULTed, so this is safe to apply BEFORE the
-- image that writes them (the 029 discipline). Verification stays null on
-- existing trips, which is honest: we genuinely do not know.
--
-- trips uses all-PascalCase quoted columns (013/017/018/024/029/037).
-- Idempotent. Database-first (no EF migrations). Run AFTER 024.
--   (use the mapcars-db skill; psql is not installed on this machine)
-- =============================================================================

ALTER TABLE trips
    ADD COLUMN IF NOT EXISTS "PinVerifiedAtUtc" TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS "PinAttemptCount"  INTEGER NOT NULL DEFAULT 0;

-- Deliberately NOT backfilled. Every existing trip keeps PinVerifiedAtUtc null,
-- because none of them were verified server-side — stamping them would invent
-- evidence, which is worse than having none.
