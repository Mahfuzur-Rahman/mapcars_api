-- =============================================================================
-- Mapcars — reveal the Payment Settings page in the admin sidebar.
--
-- RUN THIS AT DEPLOY TIME, not before: it is the one step that makes the menu
-- entry visible, and the page it points at only exists in the web build that
-- ships with Phase 3. Split out of 034 precisely so the schema could land early
-- without leaving a dead link in the sidebar for however long passed in between.
--
-- Reverse: 035_rollback.sql (hides them again).
-- Idempotent. Database-first (no EF migrations). Run AFTER 034.
--   (use the mapcars-db skill; psql is not installed on this machine)
-- =============================================================================

UPDATE menus SET is_active = TRUE WHERE id = 16;
UPDATE menus SET is_active = TRUE WHERE path = '/admin/settings/payments';
