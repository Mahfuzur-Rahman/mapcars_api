-- =============================================================================
-- Mapcars — drivers: per-driver Cash/Card override, plus the admin menu row for
-- the payment settings page.
--
-- THE COLUMNS ARE NULLABLE BOOLEAN ON PURPOSE — they are TRI-STATE.
--   NULL   follow the global PaymentSettings toggle
--   TRUE   explicitly allowed for this driver
--   FALSE  explicitly denied for this driver
-- A plain NOT NULL DEFAULT TRUE could not tell "never configured" apart from
-- "deliberately switched on", so an admin could never later change the global
-- default without silently overriding every driver's implicit setting.
--
-- Note the resolution rule the API applies (see DriverPaymentOptions): the
-- GLOBAL setting is a CEILING. An override can only ever narrow it. Once cash is
-- switched off platform-wide, no per-driver flag brings it back — otherwise
-- "cash is off" would not actually be true, which is the whole point of the
-- switch.
--
-- drivers' later additions are snake_case (see 009/011); matched here.
-- Idempotent. Database-first (no EF migrations). Run AFTER 002 and 033.
--   (use the mapcars-db skill; psql is not installed on this machine)
-- =============================================================================

ALTER TABLE drivers
    ADD COLUMN IF NOT EXISTS accepts_cash_override BOOLEAN,
    ADD COLUMN IF NOT EXISTS accepts_card_override BOOLEAN;

COMMENT ON COLUMN drivers.accepts_cash_override IS
    'Tri-state. NULL = follow the global PaymentSettings.CashEnabled; TRUE/FALSE = explicit per-driver override. The global setting is a ceiling: an override can only narrow it.';
COMMENT ON COLUMN drivers.accepts_card_override IS
    'Tri-state. NULL = follow the global PaymentSettings.CardEnabled; TRUE/FALSE = explicit per-driver override. The global setting is a ceiling: an override can only narrow it.';

-- ─── admin menu: Payment Settings (seeded HIDDEN) ───────────────────────────
-- Menu id 16 ('Settings', '/admin/settings') was seeded in 001 and has 404'd
-- ever since — there has never been a page there. Turn it into a PARENT
-- (path NULL, like ids 2/5/9/12) with the real page beneath it. AppShell already
-- renders a null-path node as an expandable group, so no web change is needed
-- beyond the page itself.
--
-- BOTH ROWS ARE SEEDED INACTIVE, and that is the point of splitting this from
-- 035. GetMenusForAdminAsync filters on IsActive, so nothing appears in the
-- sidebar until 035 runs. This script is therefore safe to apply now, ahead of
-- the code: without it the new row would be a dead link for however long passes
-- between the migration and the deploy — and as a bonus, deactivating 16 finally
-- retires the 404 it has been serving since 001.
UPDATE menus SET path = NULL, is_active = FALSE WHERE id = 16;

-- Keyed on path so re-running never duplicates (the 016 pattern).
INSERT INTO menus (name, path, icon, parent_id, sort_order, is_active)
SELECT 'Payment Settings', '/admin/settings/payments', 'credit-card', 16, 1, FALSE
WHERE NOT EXISTS (SELECT 1 FROM menus WHERE path = '/admin/settings/payments');

-- SuperAdmin only, mirroring how '/admin/fare' is gated: turning card payments
-- off platform-wide has the same blast radius as republishing the fare chart,
-- and the API enforces SuperAdmin on the PUT regardless — granting the menu to
-- plain Admins would only produce a 403 on click.
INSERT INTO role_menus (role_id, menu_id)
SELECT 1, m.id FROM menus m WHERE m.path = '/admin/settings/payments'
ON CONFLICT DO NOTHING;
