-- =============================================================================
-- Mapcars — admin menu: "Statements" under Payments (/admin/payments/statements).
--
-- Seeded INACTIVE, the 033/035 discipline: the page only exists in the web build
-- that ships with the statements API (041), and a live sidebar row pointing at a
-- page the running web build doesn't have is a 404 in the admin's face. Run
-- 043_activate_statements_menu.sql AFTER that web build is live.
--
-- Granted to SuperAdmin (1) and Admin (2) — the same roles that see Transactions
-- and Payouts, and the roles AdminStatementsController authorises.
--
-- Idempotent. Database-first (no EF migrations). Run AFTER 001.
--   (use the mapcars-db skill; psql is not installed on this machine)
-- =============================================================================

INSERT INTO menus (name, path, icon, parent_id, sort_order, is_active)
SELECT 'Statements', '/admin/payments/statements', 'file-text', 12, 3, FALSE
WHERE NOT EXISTS (SELECT 1 FROM menus WHERE path = '/admin/payments/statements');

INSERT INTO role_menus (role_id, menu_id)
SELECT r.role_id, m.id
FROM menus m CROSS JOIN (VALUES (1), (2)) AS r(role_id)
WHERE m.path = '/admin/payments/statements'
ON CONFLICT DO NOTHING;
