-- =============================================================================
-- Mapcars — document deletion requests: driver requests deletion, admin reviews.
-- Run AFTER 005_documents.sql / 011_wave1_profile_fields.sql.
--   psql -h <aiven-host> -p <port> -U avnadmin -d defaultdb -f 028_document_deletion_requests.sql
-- =============================================================================

ALTER TABLE documents
    ADD COLUMN IF NOT EXISTS is_deletion_requested BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS deletion_reason TEXT,
    ADD COLUMN IF NOT EXISTS deletion_requested_at_utc TIMESTAMPTZ;

CREATE INDEX IF NOT EXISTS "IX_documents_is_deletion_requested"
    ON documents (is_deletion_requested) WHERE is_deletion_requested = TRUE;
