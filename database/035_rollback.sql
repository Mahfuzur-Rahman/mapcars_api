-- =============================================================================
-- Mapcars — reverses 035: hides the Settings group and the Payment Settings page
-- again. Use if the Phase 3 web deploy is rolled back, so the sidebar does not
-- link somewhere that no longer exists.
-- Idempotent.
-- =============================================================================

UPDATE menus SET is_active = FALSE WHERE id = 16;
UPDATE menus SET is_active = FALSE WHERE path = '/admin/settings/payments';
