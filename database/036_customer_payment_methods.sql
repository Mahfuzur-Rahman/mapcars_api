-- =============================================================================
-- Mapcars — saved cards.
--
-- customers gains a payment-provider customer id (created lazily on the first
-- card save, never before — a customer who only pays cash never gets one), and
-- customer_payment_methods holds one row per saved card.
--
-- NOTHING HERE IS CARD DATA. No PAN, no CVC, no chargeable expiry — only the
-- opaque provider token plus enough to render "Visa •••• 4242, expires 04/28".
-- That is what keeps this application in the lightest PCI tier, and it is a hard
-- rule: if a future column here would hold a card number, the design is wrong,
-- not the column.
--
-- Column casing: customers' later additions are snake_case (009/011/023), and
-- the payments family (006/007) is snake_case with PascalCase audit columns.
-- Both conventions honoured below.
--
-- Prerequisite: 031 (the rename) — this references the customers table.
-- Idempotent. Database-first (no EF migrations). Run AFTER 031.
--   (use the mapcars-db skill; psql is not installed on this machine)
-- =============================================================================

ALTER TABLE customers
    ADD COLUMN IF NOT EXISTS stripe_customer_id VARCHAR(255);

CREATE UNIQUE INDEX IF NOT EXISTS uix_customers_stripe_customer_id
    ON customers (stripe_customer_id) WHERE stripe_customer_id IS NOT NULL;

CREATE TABLE IF NOT EXISTS customer_payment_methods (
    "Id"                     UUID         NOT NULL DEFAULT gen_random_uuid(),
    customer_id              UUID         NOT NULL,
    stripe_payment_method_id VARCHAR(255) NOT NULL,
    type                     VARCHAR(30)  NOT NULL DEFAULT 'card',
    brand                    VARCHAR(30),
    last4                    VARCHAR(4),
    exp_month                INTEGER,
    exp_year                 INTEGER,
    funding_type             VARCHAR(20),
    country                  VARCHAR(2),
    is_default               BOOLEAN      NOT NULL DEFAULT FALSE,
    is_active                BOOLEAN      NOT NULL DEFAULT TRUE,
    deactivated_at_utc       TIMESTAMPTZ,
    deactivation_reason      VARCHAR(100),
    -- The setup intent that established the off-session mandate, and when the
    -- customer accepted it: the audit trail for "was this card authenticated when
    -- it was saved?", which is what decides whether a later charge may skip
    -- authentication.
    stripe_setup_intent_id   VARCHAR(255),
    mandate_accepted_at_utc  TIMESTAMPTZ,
    "CreatedAtUtc"           TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    "UpdatedAtUtc"           TIMESTAMPTZ,
    CONSTRAINT "PK_customer_payment_methods" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_customer_payment_methods_customers_customer_id"
        FOREIGN KEY (customer_id) REFERENCES customers ("Id") ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS uix_customer_payment_methods_stripe_pm_id
    ON customer_payment_methods (stripe_payment_method_id);

-- The hot read: "this customer's usable cards".
CREATE INDEX IF NOT EXISTS "IX_customer_payment_methods_customer_id_is_active"
    ON customer_payment_methods (customer_id, is_active);

-- At most ONE default per customer, enforced by the database rather than by
-- application code — two defaults would make "which card do we charge?" a coin
-- toss at drop-off, and it would only show up as a mis-charge.
CREATE UNIQUE INDEX IF NOT EXISTS uix_customer_payment_methods_one_default
    ON customer_payment_methods (customer_id) WHERE is_default AND is_active;
