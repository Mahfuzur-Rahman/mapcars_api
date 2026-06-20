# Mapcars API — Run & Test Instructions

.NET 9 backend, **N-tier / Clean Architecture**, controller-based. This is the
**single source of truth** for all data; the web and mobile apps only talk to
this API (never directly to the database).

## Architecture (dependency direction points inward)

```
Mapcars.Api            ← Presentation: controllers, middleware, DI wiring
   ↓ depends on
Mapcars.Application    ← Business logic: services, DTOs, validators, interfaces
   ↓ depends on
Mapcars.Domain         ← Entities, enums, domain rules (no dependencies)
   ↑ implemented by
Mapcars.Infrastructure ← Data layer: EF Core DbContext, repositories, migrations
```

- **Domain** knows nothing about anyone. **Application** defines interfaces
  (`IRiderRepository`, `IUnitOfWork`); **Infrastructure** implements them with
  EF Core. The **Api** wires it together via `AddApplication()` + `AddInfrastructure()`.
- Reference vertical slice to copy for new features: **Riders**
  (`Application/Riders/**`, `Infrastructure/.../RiderRepository.cs`,
  `Api/Controllers/RidersController.cs`).

## Prerequisites (software)

| Tool | Version | Check |
|------|---------|-------|
| .NET SDK | 9.x | `dotnet --version` |
| PostgreSQL | 16 (with PostGIS) | only needed once we add the DB layer |
| Redis | 7 | only needed once we add the geo/realtime layer |

> The current scaffold runs **without** Postgres/Redis — it only exposes
> `/health` and `/api/v1/ping`. You only need the databases once those features
> are wired in.

## Accounts / API keys you will need

| Service | What for | Where the key goes | Free to start? |
|---------|----------|--------------------|----------------|
| **Mapbox** | Maps, geocoding, routing/ETA | `Mapbox:AccessToken` | Yes — free tier |
| **Stripe** | Payments + driver payouts (UK SCA/3DS2) | `Stripe:SecretKey`, `Stripe:WebhookSecret` | Yes — test mode, no charge |
| **PostgreSQL** | Source-of-truth database | `ConnectionStrings:Postgres` | Local Docker = free |
| **Redis** | Live driver locations + realtime | `Redis:Configuration` | Local Docker = free |
| **AWS** (later) | Hosting (eu-west-2 / London), RDS, ElastiCache | deploy config | Pay-as-you-go |

### How to get each

- **Mapbox:** sign up at mapbox.com → Account → *Access tokens* → copy the
  default public token (`pk....`). Create a separate secret token for
  server-side routing later.
- **Stripe:** sign up at stripe.com → toggle **Test mode** → Developers → API
  keys → copy the *Secret key* (`sk_test_...`). Webhook secret comes from
  Developers → Webhooks when you add an endpoint.

## Where secrets go (do NOT commit real keys)

`appsettings*.json` holds only **placeholders**. Put real dev keys in **.NET user
secrets** (stored outside the repo):

```powershell
cd src/Mapcars.Api
dotnet user-secrets init
dotnet user-secrets set "Mapbox:AccessToken" "pk.your_real_token"
dotnet user-secrets set "Stripe:SecretKey" "sk_test_your_real_key"
```

## Run it

```powershell
cd api/src/Mapcars.Api
dotnet run
```

Default dev URLs (see `Properties/launchSettings.json`):
- HTTP:  http://localhost:5126
- HTTPS: https://localhost:7156

## Test it (no database needed)

```powershell
curl http://localhost:5126/health        # controller, no DB
curl http://localhost:5126/api/v1/ping    # connectivity check used by web/mobile
```

OpenAPI document (used to generate the web/mobile clients):
- http://localhost:5126/openapi/v1.json

## Database: Postgres + migrations (needed for the Riders endpoints)

1. Start Postgres (PostGIS image) + Redis locally:

```powershell
docker run --name mapcars-pg -e POSTGRES_USER=mapcars -e POSTGRES_PASSWORD=mapcars_dev -e POSTGRES_DB=mapcars -p 5432:5432 -d postgis/postgis:16-3.4
docker run --name mapcars-redis -p 6379:6379 -d redis:7
```

The dev connection strings in `appsettings.Development.json` already match these.

2. Apply migrations (creates the `riders`, `drivers`, `trips` tables):

```powershell
cd api
dotnet ef database update -p src/Mapcars.Infrastructure -s src/Mapcars.Api
```

3. Test the Riders vertical slice:

```powershell
# create a rider
curl -X POST http://localhost:5126/api/v1/riders -H "Content-Type: application/json" -d "{\"fullName\":\"Ada Lovelace\",\"email\":\"ada@example.com\",\"phoneNumber\":\"+447700900123\"}"
# list riders
curl http://localhost:5126/api/v1/riders
```

### Adding a new migration later
```powershell
dotnet ef migrations add <Name> -p src/Mapcars.Infrastructure -s src/Mapcars.Api -o Persistence/Migrations
```
