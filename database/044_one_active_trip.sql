-- =============================================================================
-- Mapcars — trips: one live trip per driver, one open trip per customer.
--
-- WHAT WAS WRONG. Nothing stopped a driver accepting a second request while
-- already on a trip, or a customer booking a second ride while one was still
-- open. TripService now refuses both (AcceptAsync / CreateAsync), but a
-- check-then-write in the service can be raced: two taps landing together both
-- pass the check. These two partial unique indexes are what make it hold:
--
--   uix_trips_one_active_per_driver  — at most one trip per "DriverId" in
--                                      DriverAssigned / DriverArrived / InProgress
--   uix_trips_one_open_per_customer  — at most one trip per "CustomerId" in
--                                      Requested / DriverAssigned / DriverArrived /
--                                      InProgress
--
-- The API matches a violation of either BY NAME (TripRepository) and answers
-- with the same message as its pre-check instead of a 500 — do not rename them.
--
-- Partial, so finished trips (Completed / CancelledBy* / Expired) never collide:
-- anyone accumulates any number of those. "Status" is stored as the enum NAME
-- (EF HasConversion<string>), hence the string literals.
--
-- FAILS LOUDLY, NEVER HALF-APPLIES. Live data may already break a rule. Both
-- guards run before EITHER index is created, and each refuses with the
-- offending trip ids — so a refusal leaves the table exactly as it was. Settle
-- the listed trips (complete or cancel the stale ones) and re-run.
--
-- Safe to apply BEFORE the image that relies on it (the 029 discipline): the
-- running API never means to write a second live trip, so it only meets these
-- indexes in the race they exist to stop — where the old image answers 500 and
-- the new one answers with the pre-check's message. CREATE INDEX (not
-- CONCURRENTLY) blocks writes to trips while it builds: sub-second at current
-- volumes.
--
-- trips uses all-PascalCase quoted columns (013/017/018/024/029/037/038).
-- Idempotent. Database-first (no EF migrations). Run AFTER 031 ("CustomerId").
--   (use the mapcars-db skill; psql is not installed on this machine)
-- =============================================================================

-- ─── guards: refuse, naming the trips, before touching anything ─────────────
DO $$
DECLARE offenders text;
BEGIN
    SELECT string_agg(format('driver %s: trips %s', driver_id, trip_ids), '; ')
      INTO offenders
      FROM (SELECT "DriverId" AS driver_id,
                   string_agg("Id"::text, ', ' ORDER BY "CreatedAtUtc") AS trip_ids
              FROM trips
             WHERE "DriverId" IS NOT NULL
               AND "Status" IN ('DriverAssigned', 'DriverArrived', 'InProgress')
             GROUP BY "DriverId"
            HAVING count(*) > 1) d;

    IF offenders IS NOT NULL THEN
        RAISE EXCEPTION
          'Refusing to create uix_trips_one_active_per_driver: a driver holds more than one live trip (%). Complete or cancel the stale trip(s) and re-run.',
          offenders;
    END IF;
END $$;

DO $$
DECLARE offenders text;
BEGIN
    SELECT string_agg(format('customer %s: trips %s', customer_id, trip_ids), '; ')
      INTO offenders
      FROM (SELECT "CustomerId" AS customer_id,
                   string_agg("Id"::text, ', ' ORDER BY "CreatedAtUtc") AS trip_ids
              FROM trips
             WHERE "Status" IN ('Requested', 'DriverAssigned', 'DriverArrived', 'InProgress')
             GROUP BY "CustomerId"
            HAVING count(*) > 1) c;

    IF offenders IS NOT NULL THEN
        RAISE EXCEPTION
          'Refusing to create uix_trips_one_open_per_customer: a customer holds more than one open trip (%). Complete or cancel the stale trip(s) and re-run.',
          offenders;
    END IF;
END $$;

-- ─── the indexes ────────────────────────────────────────────────────────────
CREATE UNIQUE INDEX IF NOT EXISTS uix_trips_one_active_per_driver
    ON trips ("DriverId")
    WHERE "Status" IN ('DriverAssigned', 'DriverArrived', 'InProgress');

CREATE UNIQUE INDEX IF NOT EXISTS uix_trips_one_open_per_customer
    ON trips ("CustomerId")
    WHERE "Status" IN ('Requested', 'DriverAssigned', 'DriverArrived', 'InProgress');
