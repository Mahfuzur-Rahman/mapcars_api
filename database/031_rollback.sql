-- =============================================================================
-- Mapcars — reverses 031_rider_to_customer.sql, in the opposite order.
--
-- ─── RUN WITH THE API STOPPED, AND BEFORE REDEPLOYING THE OLD IMAGE ─────────
-- After 031, the API image and the database are ONE artifact. Roll back both or
-- neither:
--
--     docker stop mapcars-api
--     run this script
--     docker run <previous api image>
--
-- Rolling the image back WITHOUT this script leaves refresh_tokens.user_type =
-- 'customer' in front of a MintAccessTokenAsync that only knows 'rider'. Every
-- renewal 401s, the BFF clears the cookie, and every signed-in user is logged
-- out at once — a self-inflicted outage that looks exactly like a breach.
--
-- Running this script WITHOUT rolling the image back is equally wrong: the new
-- image maps onto 'customers', which this script has just renamed away.
--
-- Idempotent and resumable, same guard pattern as 031.
-- Leaves 030's widened CHECK constraints in place — run 030_rollback.sql after
-- this if you also want those narrowed back.
--   (use the mapcars-db skill; psql is not installed on this machine)
-- =============================================================================

-- ─── 7. admin sidebar ───────────────────────────────────────────────────────
UPDATE menus SET name = 'Riders'                                    WHERE id = 2 AND name = 'Customers';
UPDATE menus SET name = 'Rider List',   path = '/admin/riders'      WHERE id = 3 AND path = '/admin/customers';
UPDATE menus SET name = 'Rider Detail', path = '/admin/riders/[id]' WHERE id = 4 AND path = '/admin/customers/[id]';

-- ─── 6. trips."Status" ──────────────────────────────────────────────────────
UPDATE trips SET "Status" = 'CancelledByRider' WHERE "Status" = 'CancelledByCustomer';

-- ─── 5. the stored role literal, back to 'rider' ────────────────────────────
UPDATE error_logs         SET user_type   = 'rider' WHERE user_type   = 'customer';
UPDATE refresh_tokens     SET user_type   = 'rider' WHERE user_type   = 'customer';
UPDATE device_tokens      SET user_type   = 'rider' WHERE user_type   = 'customer';
UPDATE verification_codes SET user_type   = 'rider' WHERE user_type   = 'customer';
UPDATE trip_messages      SET sender_type = 'rider' WHERE sender_type = 'customer';
UPDATE ratings            SET rater_type  = 'rider' WHERE rater_type  = 'customer';

-- ─── 4. indexes ─────────────────────────────────────────────────────────────
DO $$
BEGIN
    IF to_regclass('public."IX_saved_places_customer_id_label"') IS NOT NULL
   AND to_regclass('public."IX_saved_places_rider_id_label"')    IS NULL THEN
        ALTER INDEX "IX_saved_places_customer_id_label" RENAME TO "IX_saved_places_rider_id_label"; END IF;

    IF to_regclass('public."IX_saved_places_customer_id"') IS NOT NULL
   AND to_regclass('public."IX_saved_places_rider_id"')    IS NULL THEN
        ALTER INDEX "IX_saved_places_customer_id" RENAME TO "IX_saved_places_rider_id"; END IF;

    IF to_regclass('public."IX_documents_customer_id"') IS NOT NULL
   AND to_regclass('public."IX_documents_rider_id"')    IS NULL THEN
        ALTER INDEX "IX_documents_customer_id" RENAME TO "IX_documents_rider_id"; END IF;

    IF to_regclass('public."IX_trips_CustomerId"') IS NOT NULL
   AND to_regclass('public."IX_trips_RiderId"')    IS NULL THEN
        ALTER INDEX "IX_trips_CustomerId" RENAME TO "IX_trips_RiderId"; END IF;

    IF to_regclass('public.uix_customers_google_sub') IS NOT NULL
   AND to_regclass('public.uix_riders_google_sub')    IS NULL THEN
        ALTER INDEX uix_customers_google_sub RENAME TO uix_riders_google_sub; END IF;

    IF to_regclass('public.uix_customers_phone') IS NOT NULL
   AND to_regclass('public.uix_riders_phone')    IS NULL THEN
        ALTER INDEX uix_customers_phone RENAME TO uix_riders_phone; END IF;

    IF to_regclass('public."IX_customers_Email"') IS NOT NULL
   AND to_regclass('public."IX_riders_Email"')    IS NULL THEN
        ALTER INDEX "IX_customers_Email" RENAME TO "IX_riders_Email"; END IF;
END $$;

-- ─── 3. constraints ─────────────────────────────────────────────────────────
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname='FK_saved_places_customers_customer_id')
   AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='FK_saved_places_riders_rider_id') THEN
        ALTER TABLE saved_places RENAME CONSTRAINT "FK_saved_places_customers_customer_id"
                                                TO "FK_saved_places_riders_rider_id"; END IF;

    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname='FK_documents_customers_customer_id')
   AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='FK_documents_riders_rider_id') THEN
        ALTER TABLE documents RENAME CONSTRAINT "FK_documents_customers_customer_id"
                                             TO "FK_documents_riders_rider_id"; END IF;

    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname='FK_trips_customers_CustomerId')
   AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='FK_trips_riders_RiderId') THEN
        ALTER TABLE trips RENAME CONSTRAINT "FK_trips_customers_CustomerId"
                                         TO "FK_trips_riders_RiderId"; END IF;

    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname='PK_customers')
   AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='PK_riders') THEN
        ALTER TABLE customers RENAME CONSTRAINT "PK_customers" TO "PK_riders"; END IF;
END $$;

-- ─── 2. foreign-key columns ─────────────────────────────────────────────────
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns
                WHERE table_schema='public' AND table_name='saved_places' AND column_name='customer_id')
   AND NOT EXISTS (SELECT 1 FROM information_schema.columns
                WHERE table_schema='public' AND table_name='saved_places' AND column_name='rider_id') THEN
        ALTER TABLE saved_places RENAME COLUMN customer_id TO rider_id; END IF;

    IF EXISTS (SELECT 1 FROM information_schema.columns
                WHERE table_schema='public' AND table_name='documents' AND column_name='customer_id')
   AND NOT EXISTS (SELECT 1 FROM information_schema.columns
                WHERE table_schema='public' AND table_name='documents' AND column_name='rider_id') THEN
        ALTER TABLE documents RENAME COLUMN customer_id TO rider_id; END IF;

    IF EXISTS (SELECT 1 FROM information_schema.columns
                WHERE table_schema='public' AND table_name='trips' AND column_name='CustomerId')
   AND NOT EXISTS (SELECT 1 FROM information_schema.columns
                WHERE table_schema='public' AND table_name='trips' AND column_name='RiderId') THEN
        ALTER TABLE trips RENAME COLUMN "CustomerId" TO "RiderId"; END IF;
END $$;

-- ─── 1. the table ───────────────────────────────────────────────────────────
DO $$
BEGIN
    IF to_regclass('public.customers') IS NOT NULL
   AND to_regclass('public.riders')    IS NULL THEN
        ALTER TABLE customers RENAME TO riders;
    END IF;
END $$;
