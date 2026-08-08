-- =============================================================================
-- Mapcars — central error log.
-- One table every surface writes to: the API (from ExceptionHandlingMiddleware),
-- the web app, the customer app and the driver app (via POST /api/v1/error-logs).
-- Read only from the admin portal (/admin/error-logs), SuperAdmin + Admin.
--
-- `source` says which app produced it, `level` how bad it is ('Error' for an
-- unhandled/500 failure, 'Warning' for a handled business rejection). Payload
-- columns are deliberately generous but capped so a runaway client can't write
-- unbounded rows.
--
-- Idempotent. Database-first (no EF migrations) — use the mapcars-db skill;
-- psql is not installed on this machine.
-- =============================================================================

CREATE TABLE IF NOT EXISTS error_logs (
    "Id"            UUID         NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,

    source          VARCHAR(20)  NOT NULL,           -- Api | Web | CustomerApp | DriverApp
    level           VARCHAR(20)  NOT NULL DEFAULT 'Error',
    message         VARCHAR(2000) NOT NULL,
    exception_type  VARCHAR(200),
    stack_trace     TEXT,

    -- Where it happened
    path            VARCHAR(500),                    -- request path or screen route
    http_method     VARCHAR(10),
    status_code     INTEGER,

    -- Who hit it (best-effort — anonymous errors are expected and fine)
    user_type       VARCHAR(20),                     -- rider | driver | admin | null
    user_id         UUID,

    -- Client context
    app_version     VARCHAR(50),
    platform        VARCHAR(50),                     -- android | ios | browser UA family
    user_agent      VARCHAR(500),
    ip_address      VARCHAR(64),
    correlation_id  VARCHAR(100),

    -- Triage
    is_resolved     BOOLEAN      NOT NULL DEFAULT FALSE,
    resolved_at_utc TIMESTAMPTZ,
    resolved_by_admin_id UUID,

    "CreatedAtUtc"  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    "UpdatedAtUtc"  TIMESTAMPTZ
);

-- The list page is "newest first, optionally filtered by source/level/resolved".
CREATE INDEX IF NOT EXISTS ix_error_logs_created    ON error_logs ("CreatedAtUtc" DESC);
CREATE INDEX IF NOT EXISTS ix_error_logs_source     ON error_logs (source, "CreatedAtUtc" DESC);
CREATE INDEX IF NOT EXISTS ix_error_logs_level      ON error_logs (level, "CreatedAtUtc" DESC);
CREATE INDEX IF NOT EXISTS ix_error_logs_unresolved ON error_logs (is_resolved, "CreatedAtUtc" DESC);

-- ─── Admin sidebar entry ─────────────────────────────────────────────────────
-- Same pattern as 016_fare_settings_menu.sql / 020_posters.sql: keyed on path
-- so re-running never duplicates the row.

INSERT INTO menus (name, path, icon, parent_id, sort_order, is_active)
SELECT 'Error Logger', '/admin/error-logs', 'alert-triangle', NULL, 10, TRUE
WHERE NOT EXISTS (SELECT 1 FROM menus WHERE path = '/admin/error-logs');

-- Visible to SuperAdmin (1) and Admin (2) only — there is no third role, and
-- riders/drivers never see the admin portal at all.
INSERT INTO role_menus (role_id, menu_id)
SELECT 1, m.id FROM menus m WHERE m.path = '/admin/error-logs'
ON CONFLICT DO NOTHING;

INSERT INTO role_menus (role_id, menu_id)
SELECT 2, m.id FROM menus m WHERE m.path = '/admin/error-logs'
ON CONFLICT DO NOTHING;
