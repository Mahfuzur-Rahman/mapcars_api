-- =============================================================================
-- Mapcars — trip_messages: in-trip chat between rider and driver.
-- One row per message, ordered by sent_at_utc. Both parties on a trip may
-- send messages at any point while the trip is active (DriverAssigned through
-- InProgress). Messages are never deleted — they're short-lived trip data.
-- Idempotent. Database-first (no EF migrations). Run AFTER 002.
--   (use the mapcars-db skill; psql is not installed on this machine)
-- =============================================================================

CREATE TABLE IF NOT EXISTS trip_messages (
    "Id"           UUID         NOT NULL DEFAULT gen_random_uuid(),
    trip_id        UUID         NOT NULL,
    sender_type    VARCHAR(10)  NOT NULL,   -- 'rider' | 'driver'
    sender_id      UUID         NOT NULL,
    content        TEXT         NOT NULL,
    sent_at_utc    TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    "CreatedAtUtc" TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    "UpdatedAtUtc" TIMESTAMPTZ,
    CONSTRAINT "PK_trip_messages" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_trip_messages_trips" FOREIGN KEY (trip_id)
        REFERENCES trips ("Id") ON DELETE RESTRICT,
    CONSTRAINT "CK_trip_messages_sender_type" CHECK (sender_type IN ('rider', 'driver'))
);

-- History queries always filter by trip + order by time.
CREATE INDEX IF NOT EXISTS "IX_trip_messages_trip_id_sent_at"
    ON trip_messages (trip_id, sent_at_utc);
