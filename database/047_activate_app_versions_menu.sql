-- =============================================================================
-- Mapcars — reveal the "App versions" admin menu row seeded by 046.
--
-- Run AT DEPLOY TIME, after the web build containing /admin/settings/app-versions
-- is live (see 046). Rollback: set is_active = FALSE on the same row.
-- =============================================================================

UPDATE menus SET is_active = TRUE WHERE path = '/admin/settings/app-versions';
