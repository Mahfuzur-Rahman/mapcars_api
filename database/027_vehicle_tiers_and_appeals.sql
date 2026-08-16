-- =============================================================================
-- Mapcars — vehicle tiers & driver tier change appeals.
-- Run AFTER 010_vehicles.sql / 011_wave1_profile_fields.sql / 016_fare_settings_menu.sql.
--   psql -h <aiven-host> -p <port> -U avnadmin -d defaultdb -f 027_vehicle_tiers_and_appeals.sql
-- =============================================================================

-- 1. Add tier column to vehicles table (default to 'economy')
ALTER TABLE vehicles
    ADD COLUMN IF NOT EXISTS tier VARCHAR(30) NOT NULL DEFAULT 'economy';

CREATE INDEX IF NOT EXISTS "IX_vehicles_tier" ON vehicles (tier);

-- 2. Create vehicle_tier_appeals table
CREATE TABLE IF NOT EXISTS vehicle_tier_appeals (
    "Id"                 UUID          NOT NULL DEFAULT gen_random_uuid(),
    driver_id            UUID          NOT NULL,
    vehicle_id           UUID          NOT NULL,
    current_tier         VARCHAR(30)   NOT NULL,
    requested_tier       VARCHAR(30)   NOT NULL,
    reason               TEXT          NOT NULL,
    photo_storage_keys   TEXT[],
    status               VARCHAR(30)   NOT NULL DEFAULT 'Pending',
    admin_notes          TEXT,
    reviewed_by_admin_id UUID,
    reviewed_at_utc      TIMESTAMPTZ,
    "CreatedAtUtc"       TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    "UpdatedAtUtc"       TIMESTAMPTZ,
    CONSTRAINT "PK_vehicle_tier_appeals" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_vehicle_tier_appeals_drivers_driver_id" FOREIGN KEY (driver_id)
        REFERENCES drivers ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_vehicle_tier_appeals_vehicles_vehicle_id" FOREIGN KEY (vehicle_id)
        REFERENCES vehicles ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_vehicle_tier_appeals_admins_reviewed_by_admin_id" FOREIGN KEY (reviewed_by_admin_id)
        REFERENCES admins (id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS "IX_vehicle_tier_appeals_driver_id" ON vehicle_tier_appeals (driver_id);
CREATE INDEX IF NOT EXISTS "IX_vehicle_tier_appeals_vehicle_id" ON vehicle_tier_appeals (vehicle_id);
CREATE INDEX IF NOT EXISTS "IX_vehicle_tier_appeals_status" ON vehicle_tier_appeals (status);

-- 3. Add Tier Appeals menu under Drivers in the admin menu hierarchy
INSERT INTO menus (name, path, icon, parent_id, sort_order, is_active)
SELECT 'Tier Appeals', '/admin/tier-appeals', 'layers', 5, 4, TRUE
WHERE NOT EXISTS (SELECT 1 FROM menus WHERE path = '/admin/tier-appeals');

-- Grant Tier Appeals to SuperAdmin (role 1) and Admin (role 2)
INSERT INTO role_menus (role_id, menu_id)
SELECT 1, m.id FROM menus m WHERE m.path = '/admin/tier-appeals'
ON CONFLICT DO NOTHING;

INSERT INTO role_menus (role_id, menu_id)
SELECT 2, m.id FROM menus m WHERE m.path = '/admin/tier-appeals'
ON CONFLICT DO NOTHING;
