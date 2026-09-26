-- =============================================================================
-- Mapcars — trips: card charge state.
--
-- "ChargeStartedAtUtc" is the important one. It is the atomic claim flag that
-- makes the charge pipeline safe: a single conditional UPDATE sets it (the same
-- idiom as TryAssignAsync / TryExpireAsync), so the attempt fired inline when a
-- trip completes and the recovery sweeper can never both charge one trip. A
-- value older than the staleness window means the claiming process died, and the
-- sweeper may re-claim — safely, because the retry reuses the same provider
-- idempotency key and gets back the intent the dead process created.
--
-- ALSO FIXES AN EXISTING LIE. Cancelled and expired trips have kept
-- PaymentStatus='Pending' forever, which reads as an unsettled fare in every
-- "what is outstanding?" query and would show every cancelled trip as owing
-- money on the admin transactions page. The backfill below settles them as
-- 'Voided'; from here on CancelAsync and TryExpireAsync write it themselves.
--
-- trips uses all-PascalCase quoted columns even for later additions (013/017/
-- 018/024/029). Every column here is nullable or DEFAULTed, so this script is
-- safe to apply BEFORE the image that uses it — the running API knows nothing
-- about these columns and a bare NOT NULL would reject its every INSERT and stop
-- bookings the moment it ran (the 029 discipline).
--
-- Prerequisite: 036, which creates customer_payment_methods (the FK below points
-- at it), and 031 before that.
-- Idempotent. Database-first (no EF migrations). Run AFTER 031 and 036.
--   (use the mapcars-db skill; psql is not installed on this machine)
-- =============================================================================

ALTER TABLE trips
    ADD COLUMN IF NOT EXISTS "StripePaymentIntentId"   VARCHAR(255),
    ADD COLUMN IF NOT EXISTS "CustomerPaymentMethodId" UUID,
    ADD COLUMN IF NOT EXISTS "ChargeStartedAtUtc"      TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS "AmountChargedPence"      INTEGER,
    ADD COLUMN IF NOT EXISTS "PaymentFailureCode"      VARCHAR(60),
    ADD COLUMN IF NOT EXISTS "PaymentFailureMessage"   VARCHAR(500),
    ADD COLUMN IF NOT EXISTS "PaymentAttemptCount"     INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "NextPaymentRetryAtUtc"   TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS "LastPaymentEventAtUtc"   TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS "PaymentWaivedAtUtc"      TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS "PaymentWaivedByAdminId"  UUID,
    -- RESERVED for a pre-auth hold at booking. Nothing writes this: the chosen
    -- model saves the card and charges at drop-off. Here so the column exists if
    -- that switch is ever flipped, and so the state machine documents where an
    -- authorisation would sit.
    ADD COLUMN IF NOT EXISTS "AuthorizedAtUtc"         TIMESTAMPTZ;

-- SET NULL, never CASCADE: deleting a saved card must not delete the trip it
-- paid for. The trip is a financial record.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
         WHERE conname = 'FK_trips_customer_payment_methods_CustomerPaymentMethodId')
    THEN
        ALTER TABLE trips
            ADD CONSTRAINT "FK_trips_customer_payment_methods_CustomerPaymentMethodId"
            FOREIGN KEY ("CustomerPaymentMethodId")
            REFERENCES customer_payment_methods ("Id") ON DELETE SET NULL;
    END IF;
END $$;

-- ─── one-time backfill: cancelled and expired trips owe nothing ─────────────
UPDATE trips
   SET "PaymentStatus" = 'Voided'
 WHERE "PaymentStatus" = 'Pending'
   AND "Status" IN ('CancelledByCustomer', 'CancelledByRider', 'CancelledByDriver', 'Expired');
-- 'CancelledByRider' is listed deliberately: this script must do the right thing
-- whether it runs before or after 031 rewrites that value.

-- ─── indexes ────────────────────────────────────────────────────────────────

-- A webhook looks a trip up by its intent, and one intent must never be attached
-- to two trips.
CREATE UNIQUE INDEX IF NOT EXISTS "IX_trips_StripePaymentIntentId"
    ON trips ("StripePaymentIntentId") WHERE "StripePaymentIntentId" IS NOT NULL;

-- "What does this customer still owe?" — the debt query.
CREATE INDEX IF NOT EXISTS "IX_trips_CustomerId_PaymentStatus"
    ON trips ("CustomerId", "PaymentStatus");

-- The recovery sweeper's stalled/never-started passes. Partial, because the rows
-- it wants are a vanishing sliver of the table.
CREATE INDEX IF NOT EXISTS "IX_trips_charge_recovery"
    ON trips ("PaymentStatus", "ChargeStartedAtUtc")
    WHERE "PaymentMethod" = 'Card' AND "PaymentStatus" IN ('Processing', 'ActionRequired');

-- The sweeper's due-retry pass.
CREATE INDEX IF NOT EXISTS "IX_trips_NextPaymentRetryAtUtc"
    ON trips ("NextPaymentRetryAtUtc") WHERE "NextPaymentRetryAtUtc" IS NOT NULL;
