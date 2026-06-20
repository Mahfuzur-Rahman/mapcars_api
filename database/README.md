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
```

All scripts are idempotent (`CREATE TABLE IF NOT EXISTS`, `ON CONFLICT DO NOTHING`),
so re-running them is safe.

| Script | Tables |
|--------|--------|
| `001_admin_auth.sql` | `roles`, `admins`, `menus`, `role_menus`, `admin_menu_permissions` (+ seed data) |
| `002_riders_drivers_trips.sql` | `riders`, `drivers`, `trips` |
| `003_verification_codes.sql` | `verification_codes` |

## Conventions

- **Casing:** base columns use `"PascalCase"` (EF default convention); auth columns
  added later use `snake_case`. Keep new columns consistent with the table they
  join, and mirror the name in the EF configuration via `HasColumnName(...)`.
- **Enums** (`drivers."Status"`, `trips."Status"`) are stored as the enum *name*
  (text), matching EF `HasConversion<string>()`.
- To add a new table: create the next numbered script, define the table, then add
  the entity (`Domain/Entities`) + configuration (`Infrastructure/.../Configurations`)
  + `DbSet` on `AppDbContext`.
