-- =============================================================================
-- Mapcars — admin menu: "App versions" under Settings (/admin/settings/app-versions).
--
-- The page edits the newest published build of each mobile app (the soft
-- "update available" prompt). The values themselves live in the existing
-- app_settings store under key 'app-versions' and are created on first read, so
-- there is NO table to add — only the menu row.
--
-- Seeded INACTIVE, the 033/035/042 discipline: the page only exists in the web
-- build that ships with this API, and a live sidebar row pointing at a page the
-- running web build doesn't have is a 404 in the admin's face. Run
-- 047_activate_app_versions_menu.sql AFTER that web build is live.
--
-- Granted to SuperAdmin (1) and Admin (2), matching AppVersionController.
-- Parent is the "Settings" row (id 16, see 034).
--
-- Idempotent. Database-first (no EF migrations). Run AFTER 034.
--   (use the mapcars-db skill; psql is not installed on this machine)
-- =============================================================================

INSERT INTO menus (name, path, icon, parent_id, sort_order, is_active)
SELECT 'App versions', '/admin/settings/app-versions', 'sliders', 16, 2, FALSE
WHERE NOT EXISTS (SELECT 1 FROM menus WHERE path = '/admin/settings/app-versions');

INSERT INTO role_menus (role_id, menu_id)
SELECT r.role_id, m.id
FROM menus m CROSS JOIN (VALUES (1), (2)) AS r(role_id)
WHERE m.path = '/admin/settings/app-versions'
ON CONFLICT DO NOTHING;
