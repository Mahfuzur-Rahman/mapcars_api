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

-- -----------------------------------------------------------------------------
-- Part 2: normalise stored sign-in phones to E.164 (+447…).
-- The apps used to save "+4407…" while the web saved "+447…", so one person
-- could hold two accounts. Rewrites only the four known mobile shapes. Anything
-- else is left alone for a manual decision (see the README for the inspection
-- query). ABORTS, changing nothing, if a rewrite would collide with an existing
-- number: that is the same person twice, and needs a human.
-- Inspected 2026-10-13: 2 customers + 2 drivers had phones, all already E.164,
-- so on that data this part is a no-op.
-- -----------------------------------------------------------------------------
BEGIN;

CREATE TEMP TABLE phone_fix ON COMMIT DROP AS
SELECT tbl, id, old, CASE
         WHEN p ~ '^\+4407[0-9]{9}$' THEN '+44' || substr(p, 5)
         WHEN p ~ '^07[0-9]{9}$'     THEN '+44' || substr(p, 2)
         WHEN p ~ '^447[0-9]{9}$'    THEN '+'   || p
         WHEN p ~ '^\+447[0-9]{9}$'  THEN p
       END AS new
FROM (
  SELECT 'customers' AS tbl, "Id" AS id, "PhoneNumber" AS old,
         regexp_replace("PhoneNumber", '[[:space:]\-\(\)\.]', '', 'g') AS p
  FROM customers WHERE "PhoneNumber" IS NOT NULL
  UNION ALL
  SELECT 'drivers', "Id", "PhoneNumber",
         regexp_replace("PhoneNumber", '[[:space:]\-\(\)\.]', '', 'g')
  FROM drivers WHERE "PhoneNumber" IS NOT NULL
) s;

DO $$
DECLARE clash int;
BEGIN
  SELECT count(*) INTO clash FROM (
    SELECT tbl, new FROM phone_fix WHERE new IS NOT NULL GROUP BY tbl, new HAVING count(*) > 1
  ) d;
  IF clash > 0 THEN
    RAISE EXCEPTION '045: % phone number(s) would collide after normalising; nothing changed. Inspect phone_fix duplicates and merge by hand.', clash;
  END IF;
END $$;

UPDATE customers c SET "PhoneNumber" = f.new
FROM phone_fix f
WHERE f.tbl = 'customers' AND f.id = c."Id" AND f.new IS NOT NULL AND f.new <> c."PhoneNumber";

UPDATE drivers d SET "PhoneNumber" = f.new
FROM phone_fix f
WHERE f.tbl = 'drivers' AND f.id = d."Id" AND f.new IS NOT NULL AND f.new <> d."PhoneNumber";

COMMIT;
