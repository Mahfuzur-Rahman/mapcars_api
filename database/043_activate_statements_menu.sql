-- =============================================================================
-- Mapcars — reveal the "Statements" admin menu row seeded by 042.
--
-- Run AT DEPLOY TIME, after the web build containing /admin/payments/statements
-- is live (see 042). Rollback: set is_active = FALSE on the same row.
-- =============================================================================

UPDATE menus SET is_active = TRUE WHERE path = '/admin/payments/statements';
