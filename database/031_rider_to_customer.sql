-- =============================================================================
-- Mapcars — Rider → Customer: the structural rename.
--
--   riders                  -> customers
--   trips."RiderId"         -> trips."CustomerId"        (quoted PascalCase)
--   documents.rider_id      -> documents.customer_id     (snake_case)
--   saved_places.rider_id   -> saved_places.customer_id  (snake_case)
--   4 constraints, 7 indexes, 6 tables of stored 'rider' values,
--   5 trips."Status" values, 3 admin menu rows
--
-- ─── RUN WITH THE API STOPPED ───────────────────────────────────────────────
-- EF maps one entity to one table name, so no image works against both 'riders'
-- and 'customers'. The sequence is:
--
--     docker pull <new api image>          # pre-pull, so the window is seconds
--     docker stop mapcars-api
--     run this script                      # sub-second at current data volumes
--     docker run <new api image>           # UserTypes.Customer = "customer"
--     smoke test (see TODO_PAYMENTS.md 1e)
--
-- Stopping the API is not just about the table name. ratings carries
-- UNIQUE (trip_id, rater_type): if the API were emitting 'customer' while old
-- rows still said 'rider', a second rating on one trip would slip past the
-- duplicate check in RatingService and insert under the other value — and the
-- UPDATE below would then collide with the duplicate it had let through,
-- rolling this entire migration back. A stopped API removes that interval.
--
-- ─── ROLLBACK IS PAIRED, NOT OPTIONAL ───────────────────────────────────────
-- After this runs, the API image and the database are ONE artifact. Roll back
-- both (031_rollback.sql with the API stopped, then the previous image) or
-- neither. Rolling back the image alone leaves refresh_tokens.user_type =
-- 'customer' in front of a MintAccessTokenAsync that only knows 'rider': every
-- renewal 401s, the BFF clears the cookie, and every signed-in user is logged
-- out at once.
--
-- ─── RE-RUNNABLE ────────────────────────────────────────────────────────────
-- ALTER ... RENAME has no IF EXISTS, so every rename sits in a DO block guarded
-- on (old exists AND new does not). Running this twice is a no-op, and running
-- it after a partial failure resumes exactly where it stopped.
--
-- ─── DELIBERATELY NOT TOUCHED ───────────────────────────────────────────────
--   * "CK_documents_exactly_one_owner" — its NAME carries no 'rider', and
--     Postgres stores the expression against column attnums, so its body
--     re-renders as customer_id automatically when the column is renamed.
--     Verified live before writing this.
--   * error_logs rows holding 'Admin'/'SuperAdmin' — a pre-existing casing
--     inconsistency, unrelated to this rename.
--
-- Prerequisite: 030 must already have run (it widens the CHECK constraints that
-- would otherwise reject the 'customer' values written below).
-- Idempotent. Database-first (no EF migrations).
--   (use the mapcars-db skill; psql is not installed on this machine)
-- =============================================================================

-- ─── 0. refuse to run before 030 ────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
         WHERE conname = 'CK_ratings_rater_type'
           AND pg_get_constraintdef(oid) LIKE '%customer%')
    THEN
        RAISE EXCEPTION
          'Run 030_user_type_dual_accept.sql first: the CHECK constraints still reject ''customer''.';
    END IF;
END $$;

-- ─── 1. the table ───────────────────────────────────────────────────────────
DO $$
BEGIN
    IF to_regclass('public.riders')    IS NOT NULL
   AND to_regclass('public.customers') IS NULL THEN
        ALTER TABLE riders RENAME TO customers;
    END IF;
END $$;

-- ─── 2. foreign-key columns (two casings — quote the PascalCase one) ────────
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns
                WHERE table_schema='public' AND table_name='trips' AND column_name='RiderId')
   AND NOT EXISTS (SELECT 1 FROM information_schema.columns
                WHERE table_schema='public' AND table_name='trips' AND column_name='CustomerId') THEN
        ALTER TABLE trips RENAME COLUMN "RiderId" TO "CustomerId";
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.columns
                WHERE table_schema='public' AND table_name='documents' AND column_name='rider_id')
   AND NOT EXISTS (SELECT 1 FROM information_schema.columns
                WHERE table_schema='public' AND table_name='documents' AND column_name='customer_id') THEN
        ALTER TABLE documents RENAME COLUMN rider_id TO customer_id;
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.columns
                WHERE table_schema='public' AND table_name='saved_places' AND column_name='rider_id')
   AND NOT EXISTS (SELECT 1 FROM information_schema.columns
                WHERE table_schema='public' AND table_name='saved_places' AND column_name='customer_id') THEN
        ALTER TABLE saved_places RENAME COLUMN rider_id TO customer_id;
    END IF;
END $$;

-- ─── 3. constraints (cosmetic — a FK follows the OID, not the name) ─────────
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname='PK_riders')
   AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='PK_customers') THEN
        ALTER TABLE customers RENAME CONSTRAINT "PK_riders" TO "PK_customers";
    END IF;

    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname='FK_trips_riders_RiderId')
   AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='FK_trips_customers_CustomerId') THEN
        ALTER TABLE trips RENAME CONSTRAINT "FK_trips_riders_RiderId"
                                         TO "FK_trips_customers_CustomerId";
    END IF;

    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname='FK_documents_riders_rider_id')
   AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='FK_documents_customers_customer_id') THEN
        ALTER TABLE documents RENAME CONSTRAINT "FK_documents_riders_rider_id"
                                             TO "FK_documents_customers_customer_id";
    END IF;

    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname='FK_saved_places_riders_rider_id')
   AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='FK_saved_places_customers_customer_id') THEN
        ALTER TABLE saved_places RENAME CONSTRAINT "FK_saved_places_riders_rider_id"
                                                TO "FK_saved_places_customers_customer_id";
    END IF;
END $$;

-- ─── 4. indexes (the PK's backing index renamed with its constraint above) ──
DO $$
BEGIN
    IF to_regclass('public."IX_riders_Email"')    IS NOT NULL
   AND to_regclass('public."IX_customers_Email"') IS NULL THEN
        ALTER INDEX "IX_riders_Email" RENAME TO "IX_customers_Email"; END IF;

    IF to_regclass('public.uix_riders_phone')    IS NOT NULL
   AND to_regclass('public.uix_customers_phone') IS NULL THEN
        ALTER INDEX uix_riders_phone RENAME TO uix_customers_phone; END IF;

    IF to_regclass('public.uix_riders_google_sub')    IS NOT NULL
   AND to_regclass('public.uix_customers_google_sub') IS NULL THEN
        ALTER INDEX uix_riders_google_sub RENAME TO uix_customers_google_sub; END IF;

    IF to_regclass('public."IX_trips_RiderId"')    IS NOT NULL
   AND to_regclass('public."IX_trips_CustomerId"') IS NULL THEN
        ALTER INDEX "IX_trips_RiderId" RENAME TO "IX_trips_CustomerId"; END IF;

    IF to_regclass('public."IX_documents_rider_id"')    IS NOT NULL
   AND to_regclass('public."IX_documents_customer_id"') IS NULL THEN
        ALTER INDEX "IX_documents_rider_id" RENAME TO "IX_documents_customer_id"; END IF;

    IF to_regclass('public."IX_saved_places_rider_id"')    IS NOT NULL
   AND to_regclass('public."IX_saved_places_customer_id"') IS NULL THEN
        ALTER INDEX "IX_saved_places_rider_id" RENAME TO "IX_saved_places_customer_id"; END IF;

    -- Functional index on (rider_id, lower(label)); the expression follows the
    -- column rename automatically, only the index name needs changing.
    IF to_regclass('public."IX_saved_places_rider_id_label"')    IS NOT NULL
   AND to_regclass('public."IX_saved_places_customer_id_label"') IS NULL THEN
        ALTER INDEX "IX_saved_places_rider_id_label" RENAME TO "IX_saved_places_customer_id_label"; END IF;
END $$;

-- ─── 5. the literal 'rider' stored as DATA, in six tables ───────────────────
-- Legal only because 030 widened the three CHECK constraints first. Safe to do
-- in one pass only because the API is stopped (see the ratings note at the top).
UPDATE ratings            SET rater_type  = 'customer' WHERE rater_type  = 'rider';
UPDATE trip_messages      SET sender_type = 'customer' WHERE sender_type = 'rider';
UPDATE verification_codes SET user_type   = 'customer' WHERE user_type   = 'rider';
-- device_tokens is the one that fails SILENTLY if missed: DeviceTokenRepository
-- filters on user_type, so an un-migrated row simply stops matching and every
-- push to that customer stops — no exception, nothing in the logs.
UPDATE device_tokens      SET user_type   = 'customer' WHERE user_type   = 'rider';
-- refresh_tokens is what makes an image-only rollback a mass logout.
UPDATE refresh_tokens     SET user_type   = 'customer' WHERE user_type   = 'rider';
-- error_logs is an append-only audit trail with no CHECK and no query filter;
-- rewritten only so the admin Error Logger groups consistently.
UPDATE error_logs         SET user_type   = 'customer' WHERE user_type   = 'rider';

-- ─── 6. the SEVENTH data site: trips."Status" ───────────────────────────────
-- TripStatus is persisted by name (HasConversion<string>), so every trip a
-- passenger cancelled holds the literal 'CancelledByRider'. This is separate
-- from the role literal above and is easy to miss entirely.
-- VARCHAR(30); 'CancelledByCustomer' is 19 chars, so it fits.
UPDATE trips SET "Status" = 'CancelledByCustomer' WHERE "Status" = 'CancelledByRider';

-- ─── 7. admin sidebar (menu labels and paths are DB rows, seeded by 001) ────
UPDATE menus SET name = 'Customers'                                       WHERE id = 2 AND name = 'Riders';
UPDATE menus SET name = 'Customer List',   path = '/admin/customers'      WHERE id = 3 AND path = '/admin/riders';
UPDATE menus SET name = 'Customer Detail', path = '/admin/customers/[id]' WHERE id = 4 AND path = '/admin/riders/[id]';
