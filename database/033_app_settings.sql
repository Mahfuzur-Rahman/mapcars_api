-- =============================================================================
-- Mapcars — app_settings: a generic key → JSONB settings store, append-only and
-- versioned. Same shape as fare_charts (008): one row per published version, and
-- the current value for a key is simply its highest version.
--
-- WHY NOT JUST ADD THE TOGGLES TO THE FARE CHART.
-- The fare chart already has a Redis+Postgres store, a SuperAdmin-gated PUT and
-- an admin UI, so bolting payment toggles onto it is tempting. But fare_charts
-- is a PRICING AUDIT TRAIL — every row answers "what were we charging on the
-- 14th?". Minting a new pricing version every time someone flips a payment
-- checkbox destroys the one thing that history is for. A separate store also
-- finally gives the '/admin/settings' menu row seeded back in 001 something to
-- point at, and somewhere for later settings to land.
--
-- Seeds payments v1 with CARD OFF. The kill switch must exist, and be closed,
-- before any code that can charge a customer is deployed.
--
-- Idempotent. Database-first (no EF migrations). Run AFTER 001.
--   (use the mapcars-db skill; psql is not installed on this machine)
-- =============================================================================

CREATE TABLE IF NOT EXISTS app_settings (
    "Id"                UUID          NOT NULL DEFAULT gen_random_uuid(),
    key                 VARCHAR(100)  NOT NULL,
    version             INTEGER       NOT NULL,
    payload_json        JSONB         NOT NULL,
    updated_by_admin_id UUID,
    "CreatedAtUtc"      TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    "UpdatedAtUtc"      TIMESTAMPTZ,
    CONSTRAINT "PK_app_settings" PRIMARY KEY ("Id")
);

-- One row per (key, version). The store computes the next version inside its
-- write gate; this index is what stops two API instances minting the same one.
CREATE UNIQUE INDEX IF NOT EXISTS "IX_app_settings_key_version"
    ON app_settings (key, version);

-- The read path is always "newest version for this key".
CREATE INDEX IF NOT EXISTS "IX_app_settings_key_version_desc"
    ON app_settings (key, version DESC);

-- Seed v1 of the payment settings. Keyed on the key, so re-running never
-- duplicates and never overwrites a value an admin has since published.
INSERT INTO app_settings (key, version, payload_json)
SELECT 'payments', 1, '{
  "cashEnabled": true,
  "cardEnabled": false,
  "defaultMethod": "Cash"
}'::jsonb
WHERE NOT EXISTS (SELECT 1 FROM app_settings WHERE key = 'payments');
