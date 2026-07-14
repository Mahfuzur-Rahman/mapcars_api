-- =============================================================================
-- Mapcars — payouts: history of Stripe payouts sent to a driver's connected account
-- Run AFTER 006_driver_payout_accounts.sql.
--   psql -h <aiven-host> -p <port> -U avnadmin -d defaultdb -f 007_payouts.sql
-- =============================================================================

CREATE TABLE IF NOT EXISTS payouts (
    "Id"             UUID             NOT NULL DEFAULT gen_random_uuid(),
    driver_id        UUID             NOT NULL,
    stripe_payout_id VARCHAR(255)     NOT NULL,
    amount           NUMERIC(10,2)    NOT NULL,
    currency         VARCHAR(3)       NOT NULL,
    status           VARCHAR(30)      NOT NULL DEFAULT 'Pending',
    arrived_at_utc   TIMESTAMPTZ,
    "CreatedAtUtc"   TIMESTAMPTZ      NOT NULL DEFAULT NOW(),
    "UpdatedAtUtc"   TIMESTAMPTZ,
    CONSTRAINT "PK_payouts" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_payouts_drivers_driver_id" FOREIGN KEY (driver_id)
        REFERENCES drivers ("Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX IF NOT EXISTS uix_payouts_stripe_payout_id
    ON payouts (stripe_payout_id);
CREATE INDEX IF NOT EXISTS "IX_payouts_driver_id" ON payouts (driver_id);
