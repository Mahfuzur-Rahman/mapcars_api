-- =============================================================================
-- Mapcars — admin menu: Fare Settings
-- Adds the "/admin/fare" admin-portal page to the dynamic sidebar and grants it
-- to SuperAdmin (role 1), who is the only role allowed to publish a fare chart.
-- Idempotent: keyed on the menu path, safe to re-run. Database-first (no EF).
-- =============================================================================

-- The sequence is at MAX(id) from the seed (001), so a plain INSERT gets the
-- next id. Keyed on path so re-running never duplicates the row.
INSERT INTO menus (name, path, icon, parent_id, sort_order, is_active)
SELECT 'Fare Settings', '/admin/fare', 'sliders', NULL, 8, TRUE
WHERE NOT EXISTS (SELECT 1 FROM menus WHERE path = '/admin/fare');

-- Grant to SuperAdmin only (fare publishing is SuperAdmin-gated at the API).
INSERT INTO role_menus (role_id, menu_id)
SELECT 1, m.id FROM menus m WHERE m.path = '/admin/fare'
ON CONFLICT DO NOTHING;
