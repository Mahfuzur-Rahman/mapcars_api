# Mapcars — Database (database-first)

The API uses a **database-first** approach. These SQL scripts are the **single
source of truth** for the schema. There are **no EF Core migrations** — to change
the schema, edit (or add) a script here and run it.

EF Core is used only to *map* C# entities onto these existing tables, via the
configurations in
`src/Mapcars.Infrastructure/Persistence/Configurations/*`. When you change a
table here, update the matching entity + configuration so they stay in sync.

## Run order

Scripts are numbered and must be run in order against a fresh `mapcars` database:

```bash
psql -U postgres -d mapcars -f 001_admin_auth.sql
psql -U postgres -d mapcars -f 002_riders_drivers_trips.sql
psql -U postgres -d mapcars -f 003_verification_codes.sql
psql -U postgres -d mapcars -f 004_verification_codes_sent_via.sql
psql -U postgres -d mapcars -f 005_documents.sql
psql -U postgres -d mapcars -f 006_driver_payout_accounts.sql
psql -U postgres -d mapcars -f 007_payouts.sql
psql -U postgres -d mapcars -f 008_fare_charts_and_trip_pricing.sql
psql -U postgres -d mapcars -f 009_driver_profile_fields.sql
psql -U postgres -d mapcars -f 010_vehicles.sql
psql -U postgres -d mapcars -f 011_wave1_profile_fields.sql
psql -U postgres -d mapcars -f 012_saved_places.sql
psql -U postgres -d mapcars -f 013_trip_lifecycle.sql
psql -U postgres -d mapcars -f 014_ratings.sql
psql -U postgres -d mapcars -f 015_driver_verification_documents.sql
psql -U postgres -d mapcars -f 016_fare_settings_menu.sql
psql -U postgres -d mapcars -f 017_trip_tip.sql
```

All scripts are idempotent (`CREATE TABLE IF NOT EXISTS`, `ON CONFLICT DO NOTHING`),
so re-running them is safe.

| Script | Tables |
|--------|--------|
| `001_admin_auth.sql` | `roles`, `admins`, `menus`, `role_menus`, `admin_menu_permissions` (+ seed data) |
| `002_riders_drivers_trips.sql` | `riders`, `drivers`, `trips` |
| `003_verification_codes.sql` | `verification_codes` |
| `004_verification_codes_sent_via.sql` | `verification_codes` (+ `sent_via` column, cleanup index) |
| `005_documents.sql` | `documents` |
| `006_driver_payout_accounts.sql` | `driver_payout_accounts` |
| `007_payouts.sql` | `payouts` |
| `008_fare_charts_and_trip_pricing.sql` | `fare_charts` (+ fare breakdown columns on `trips`) |
| `009_driver_profile_fields.sql` | `drivers` (+ name/DOB/address/national ID/profile picture columns) |
| `010_vehicles.sql` | `vehicles` (one per driver; make/model/year/colour/registration) |
| `011_wave1_profile_fields.sql` | `riders`, `drivers` (+ emergency contact/marketing consent/accessibility columns), `vehicles` (+ PHV plate/council), `documents` (+ expiry) |
| `012_saved_places.sql` | `saved_places` (rider's saved Home/Work/custom addresses) |
| `013_trip_lifecycle.sql` | `trips` (+ completion/cancellation columns), `riders`/`drivers` (+ cancellation/no-show counters), `drivers` (+ online/offline toggle) |
| `014_ratings.sql` | `ratings` (rider↔driver 1-5 star review per trip), `riders`/`drivers` (+ average rating/rating count) |
| `015_driver_verification_documents.sql` | `drivers` (+ `passport_number`); new `DocumentType`s (Passport, DrivingLicence, VehicleBadge, BankStatement) live in app code only |
| `016_fare_settings_menu.sql` | `menus`, `role_menus` (+ "Fare Settings" `/admin/fare` sidebar entry, granted to SuperAdmin) |
| `017_trip_tip.sql` | `trips` (+ `TipAmount` — rider tip for the broadcast dispatch model, 100% to driver) |
| `018_trip_payment.sql` | `trips` (+ `PaymentMethod`/`PaymentStatus`/`PaidAtUtc` — cash settled at completion; Stripe card capture is the next step) |
| `019_device_tokens.sql` | `device_tokens` (FCM registration tokens per rider/driver device, for push) |
| `020_posters.sql` | `posters`, `menus`, `role_menus` (+ "Posters" `/admin/posters` sidebar entry, granted to SuperAdmin + Admin) |
| `021_error_logs.sql` | `error_logs` (central error log — API, web and both apps write here), `menus`, `role_menus` (+ "Error Logger" `/admin/error-logs` entry, granted to SuperAdmin + Admin) |

## Conventions

- **Casing:** base columns use `"PascalCase"` (EF default convention); auth columns
  added later use `snake_case`. Keep new columns consistent with the table they
  join, and mirror the name in the EF configuration via `HasColumnName(...)`.
- **Enums** (`drivers."Status"`, `trips."Status"`) are stored as the enum *name*
  (text), matching EF `HasConversion<string>()`.
- To add a new table: create the next numbered script, define the table, then add
  the entity (`Domain/Entities`) + configuration (`Infrastructure/.../Configurations`)
  + `DbSet` on `AppDbContext`.
