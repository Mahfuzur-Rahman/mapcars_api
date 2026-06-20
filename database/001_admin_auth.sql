-- =============================================================================
-- Mapcars — Admin Authentication Schema
-- Run against your PostgreSQL database BEFORE starting the API.
--   psql -U postgres -d mapcars -f 001_admin_auth.sql
-- =============================================================================

-- ─── Tables ──────────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS roles (
    id          SERIAL       PRIMARY KEY,
    name        VARCHAR(50)  NOT NULL UNIQUE,
    description TEXT,
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS admins (
    id            UUID         NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    email         VARCHAR(255) NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    full_name     VARCHAR(100) NOT NULL,
    role_id       INT          NOT NULL REFERENCES roles(id),
    is_active     BOOLEAN      NOT NULL DEFAULT TRUE,
    created_by    UUID         REFERENCES admins(id),
    created_at    TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at    TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS menus (
    id          SERIAL        PRIMARY KEY,
    name        VARCHAR(100)  NOT NULL,
    path        VARCHAR(255),
    icon        VARCHAR(50),
    parent_id   INT           REFERENCES menus(id),
    sort_order  INT           NOT NULL DEFAULT 0,
    is_active   BOOLEAN       NOT NULL DEFAULT TRUE
);

-- Default access per role (SuperAdmin / Admin baseline)
CREATE TABLE IF NOT EXISTS role_menus (
    role_id INT NOT NULL REFERENCES roles(id) ON DELETE CASCADE,
    menu_id INT NOT NULL REFERENCES menus(id) ON DELETE CASCADE,
    PRIMARY KEY (role_id, menu_id)
);

-- Per-admin overrides — SuperAdmin can grant or revoke individual menus
CREATE TABLE IF NOT EXISTS admin_menu_permissions (
    admin_id   UUID    NOT NULL REFERENCES admins(id) ON DELETE CASCADE,
    menu_id    INT     NOT NULL REFERENCES menus(id)  ON DELETE CASCADE,
    is_allowed BOOLEAN NOT NULL DEFAULT TRUE,
    PRIMARY KEY (admin_id, menu_id)
);

-- ─── Seed: Roles ─────────────────────────────────────────────────────────────

INSERT INTO roles (name, description) VALUES
    ('SuperAdmin', 'Full access — can manage other admins and all menus'),
    ('Admin',      'Standard access based on assigned menus')
ON CONFLICT (name) DO NOTHING;

-- ─── Seed: Menus ─────────────────────────────────────────────────────────────

INSERT INTO menus (id, name, path, icon, parent_id, sort_order, is_active) VALUES
    (1,  'Dashboard',    '/admin',                 'layout-dashboard', NULL, 1,  TRUE),
    (2,  'Riders',       NULL,                      'users',            NULL, 2,  TRUE),
    (3,  'Rider List',   '/admin/riders',           'list',             2,    1,  TRUE),
    (4,  'Rider Detail', '/admin/riders/[id]',      'user',             2,    2,  TRUE),
    (5,  'Drivers',      NULL,                      'car',              NULL, 3,  TRUE),
    (6,  'Driver List',  '/admin/drivers',          'list',             5,    1,  TRUE),
    (7,  'Driver Detail','/admin/drivers/[id]',     'user',             5,    2,  TRUE),
    (8,  'Documents',    '/admin/drivers/documents','file-text',        5,    3,  TRUE),
    (9,  'Trips',        NULL,                      'map',              NULL, 4,  TRUE),
    (10, 'Live Map',     '/admin/trips/live',       'map-pin',          9,    1,  TRUE),
    (11, 'Trip History', '/admin/trips/history',    'clock',            9,    2,  TRUE),
    (12, 'Payments',     NULL,                      'credit-card',      NULL, 5,  TRUE),
    (13, 'Transactions', '/admin/payments/transactions','receipt',      12,   1,  TRUE),
    (14, 'Payouts',      '/admin/payments/payouts', 'banknote',         12,   2,  TRUE),
    (15, 'Admin Users',  '/admin/admins',           'shield',           NULL, 6,  TRUE),
    (16, 'Settings',     '/admin/settings',         'settings',         NULL, 7,  TRUE)
ON CONFLICT (id) DO NOTHING;

SELECT setval('menus_id_seq', (SELECT MAX(id) FROM menus));

-- ─── Seed: Role → Menu access ────────────────────────────────────────────────

-- SuperAdmin (id=1) gets all menus
INSERT INTO role_menus (role_id, menu_id)
SELECT 1, id FROM menus
ON CONFLICT DO NOTHING;

-- Admin (id=2) gets all menus EXCEPT Admin Users (id=15)
INSERT INTO role_menus (role_id, menu_id)
SELECT 2, id FROM menus WHERE id != 15
ON CONFLICT DO NOTHING;
