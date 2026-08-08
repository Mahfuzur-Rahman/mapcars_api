-- =============================================================================
-- Mapcars — central email log.
-- One row per IEmailService.SendAsync call, written by the LoggingEmailService
-- decorator that wraps whichever provider (Resend/Smtp/Console) is configured
-- — see Mapcars.Infrastructure/Services/LoggingEmailService.cs. Covers both
-- automatic system sends (OTP codes, admin welcome emails — category 'System')
-- and admin-composed ad-hoc mail (category 'Compose').
--
-- Resend's API has no bulk "list sent emails" endpoint, so this table is the
-- only place "every email we've sent" can be read from — not just a nice-to-have.
--
-- Read only from the admin portal (/admin/emails), SuperAdmin + Admin.
--
-- Idempotent. Database-first (no EF migrations) — use the mapcars-db skill;
-- psql is not installed on this machine.
-- =============================================================================

CREATE TABLE IF NOT EXISTS email_log (
    "Id"             UUID          NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,

    to_email         VARCHAR(320)  NOT NULL,
    from_address     VARCHAR(320)  NOT NULL,
    from_name        VARCHAR(200),

    subject          VARCHAR(500)  NOT NULL,
    body_html        TEXT          NOT NULL,

    provider         VARCHAR(20)   NOT NULL,           -- resend | smtp | console
    category         VARCHAR(50)   NOT NULL DEFAULT 'System', -- System | Compose
    status           VARCHAR(20)   NOT NULL,            -- Sent | Failed
    error_message    VARCHAR(2000),

    sent_by_admin_id UUID,                              -- set only for category = 'Compose'

    "CreatedAtUtc"   TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    "UpdatedAtUtc"   TIMESTAMPTZ
);

-- The list page is "newest first, optionally filtered by category/status".
CREATE INDEX IF NOT EXISTS ix_email_log_created  ON email_log ("CreatedAtUtc" DESC);
CREATE INDEX IF NOT EXISTS ix_email_log_category ON email_log (category, "CreatedAtUtc" DESC);
CREATE INDEX IF NOT EXISTS ix_email_log_status   ON email_log (status, "CreatedAtUtc" DESC);

-- ─── Admin sidebar entry ─────────────────────────────────────────────────────
-- Same pattern as 021_error_logs.sql: keyed on path so re-running never duplicates the row.

INSERT INTO menus (name, path, icon, parent_id, sort_order, is_active)
SELECT 'Email', '/admin/emails', 'mail', NULL, 11, TRUE
WHERE NOT EXISTS (SELECT 1 FROM menus WHERE path = '/admin/emails');

-- Visible to SuperAdmin (1) and Admin (2) only — there is no third role.
INSERT INTO role_menus (role_id, menu_id)
SELECT 1, m.id FROM menus m WHERE m.path = '/admin/emails'
ON CONFLICT DO NOTHING;

INSERT INTO role_menus (role_id, menu_id)
SELECT 2, m.id FROM menus m WHERE m.path = '/admin/emails'
ON CONFLICT DO NOTHING;
