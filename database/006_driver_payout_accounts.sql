-- =============================================================================
-- Mapcars — driver_payout_accounts: one Stripe Connect Express account per driver
-- Run AFTER 002_riders_drivers_trips.sql.
--   psql -h <aiven-host> -p <port> -U avnadmin -d defaultdb -f 006_driver_payout_accounts.sql
-- =============================================================================

CREATE TABLE IF NOT EXISTS driver_payout_accounts (
    "Id"              UUID             NOT NULL DEFAULT gen_random_uuid(),
    driver_id         UUID             NOT NULL,
    stripe_account_id VARCHAR(255)     NOT NULL,
    status            VARCHAR(30)      NOT NULL DEFAULT 'NotStarted',
    payouts_enabled   BOOLEAN          NOT NULL DEFAULT FALSE,
    charges_enabled   BOOLEAN          NOT NULL DEFAULT FALSE,
    "CreatedAtUtc"    TIMESTAMPTZ      NOT NULL DEFAULT NOW(),
    "UpdatedAtUtc"    TIMESTAMPTZ,
    CONSTRAINT "PK_driver_payout_accounts" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_driver_payout_accounts_drivers_driver_id" FOREIGN KEY (driver_id)
        REFERENCES drivers ("Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX IF NOT EXISTS uix_driver_payout_accounts_driver_id
    ON driver_payout_accounts (driver_id);
CREATE UNIQUE INDEX IF NOT EXISTS uix_driver_payout_accounts_stripe_account_id
    ON driver_payout_accounts (stripe_account_id);
