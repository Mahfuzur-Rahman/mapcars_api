-- =============================================================================
-- Mapcars — driver_statements + driver_statement_lines: the weekly money story.
--
-- One statement per driver per UK week (Monday 00:00 Europe/London), built once
-- the week has closed. It answers the two questions a driver actually asks —
-- "what am I owed?" and "what do I owe?" — and is what a payout and a cash
-- commission invoice will both point at (TODO_PAYMENTS.md phase 5).
--
-- ALL MONEY IS INTEGER PENCE. Trip money is NUMERIC(10,2) pounds that began life
-- as pence; statements go back to pence so a week of sums can't drift a penny.
--
-- SIGNED LINES. Each line's amount_pence is money flowing platform → driver:
--   card trip  →  + (driver_earnings + tip)   we collected the fare, we owe them
--   cash trip  →  − commission                they hold the cash, they owe us
-- A statement's net_pence is the sum: positive = we pay the driver, negative =
-- the driver owes the platform. The platform absorbs failed card charges, so a
-- card line is owed to the driver whatever the customer's payment status.
--
-- IDEMPOTENT BY CONSTRUCTION. Unique (driver_id, period_start_utc) means the
-- weekly job can run as often as it likes; unique (trip_id) on lines means no
-- trip is ever counted on two statements.
--
-- Additive, so safe to apply BEFORE the image that uses it (the 029
-- discipline). The new image REQUIRES it — its hourly job queries these tables.
--
-- snake_case feature columns, PascalCase base columns (the 006/007/036
-- convention). Idempotent. Database-first (no EF migrations). Run AFTER 031.
--   (use the mapcars-db skill; psql is not installed on this machine)
-- =============================================================================

CREATE TABLE IF NOT EXISTS driver_statements (
    "Id"                        UUID         NOT NULL DEFAULT gen_random_uuid(),
    driver_id                   UUID         NOT NULL,
    period_start_utc            TIMESTAMPTZ  NOT NULL,
    period_end_utc              TIMESTAMPTZ  NOT NULL,
    trip_count                  INTEGER      NOT NULL DEFAULT 0,
    gross_fares_pence           INTEGER      NOT NULL DEFAULT 0,
    tips_pence                  INTEGER      NOT NULL DEFAULT 0,
    commission_pence            INTEGER      NOT NULL DEFAULT 0,
    driver_earnings_pence       INTEGER      NOT NULL DEFAULT 0,
    -- Card trips: driver share + tips, collected by the platform, owed to the driver.
    card_payable_pence          INTEGER      NOT NULL DEFAULT 0,
    -- Cash trips: commission on fares the driver collected in person, owed to us.
    cash_commission_owed_pence  INTEGER      NOT NULL DEFAULT 0,
    net_pence                   INTEGER      NOT NULL DEFAULT 0,
    status                      VARCHAR(20)  NOT NULL DEFAULT 'Issued',
    "CreatedAtUtc"              TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    "UpdatedAtUtc"              TIMESTAMPTZ,
    CONSTRAINT "PK_driver_statements" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_driver_statements_drivers_driver_id"
        FOREIGN KEY (driver_id) REFERENCES drivers ("Id") ON DELETE CASCADE,
    CONSTRAINT "CK_driver_statements_period" CHECK (period_end_utc > period_start_utc),
    CONSTRAINT "CK_driver_statements_status" CHECK (status IN ('Issued', 'Settled'))
);

CREATE UNIQUE INDEX IF NOT EXISTS uix_driver_statements_driver_period
    ON driver_statements (driver_id, period_start_utc);

CREATE INDEX IF NOT EXISTS "IX_driver_statements_period_start_utc"
    ON driver_statements (period_start_utc);

CREATE TABLE IF NOT EXISTS driver_statement_lines (
    "Id"                   UUID         NOT NULL DEFAULT gen_random_uuid(),
    statement_id           UUID         NOT NULL,
    trip_id                UUID         NOT NULL,
    completed_at_utc       TIMESTAMPTZ  NOT NULL,
    payment_method         VARCHAR(20)  NOT NULL,
    fare_pence             INTEGER      NOT NULL,
    tip_pence              INTEGER      NOT NULL,
    commission_pence       INTEGER      NOT NULL,
    driver_earnings_pence  INTEGER      NOT NULL,
    amount_pence           INTEGER      NOT NULL,
    "CreatedAtUtc"         TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    "UpdatedAtUtc"         TIMESTAMPTZ,
    CONSTRAINT "PK_driver_statement_lines" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_driver_statement_lines_statements_statement_id"
        FOREIGN KEY (statement_id) REFERENCES driver_statements ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_driver_statement_lines_trips_trip_id"
        FOREIGN KEY (trip_id) REFERENCES trips ("Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX IF NOT EXISTS uix_driver_statement_lines_trip_id
    ON driver_statement_lines (trip_id);

CREATE INDEX IF NOT EXISTS "IX_driver_statement_lines_statement_id"
    ON driver_statement_lines (statement_id);
