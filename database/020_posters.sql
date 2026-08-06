-- =============================================================================
-- Mapcars — landing-page posters.
-- Admin-managed promo banners shown on the public landing page (between Hero
-- and Coverage). Up to 3 active posters render as a static row; more than 3
-- and the section becomes an auto-rotating carousel — that logic lives in the
-- web app, this table just stores the content + display order.
-- Idempotent. Database-first (no EF migrations) — use the mapcars-db skill;
-- psql is not installed on this machine.
-- =============================================================================

CREATE TABLE IF NOT EXISTS posters (
    "Id"           UUID         NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    storage_key    VARCHAR(260) NOT NULL,
    content_type   VARCHAR(100) NOT NULL,
    title          VARCHAR(200),
    subtitle       VARCHAR(300),
    link_url       VARCHAR(2048),
    sort_order     INTEGER      NOT NULL DEFAULT 0,
    is_active      BOOLEAN      NOT NULL DEFAULT TRUE,
    created_by_admin_id UUID,
    "CreatedAtUtc" TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    "UpdatedAtUtc" TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS ix_posters_active_sort ON posters (is_active, sort_order);

-- Admin sidebar entry, following 016_fare_settings_menu.sql's exact pattern.
INSERT INTO menus (name, path, icon, parent_id, sort_order, is_active)
SELECT 'Posters', '/admin/posters', 'image', NULL, 9, TRUE
WHERE NOT EXISTS (SELECT 1 FROM menus WHERE path = '/admin/posters');

-- Grant to both SuperAdmin (1) and Admin (2) — poster management isn't
-- SuperAdmin-gated the way fare publishing is.
INSERT INTO role_menus (role_id, menu_id)
SELECT 1, m.id FROM menus m WHERE m.path = '/admin/posters'
ON CONFLICT DO NOTHING;

INSERT INTO role_menus (role_id, menu_id)
SELECT 2, m.id FROM menus m WHERE m.path = '/admin/posters'
ON CONFLICT DO NOTHING;
