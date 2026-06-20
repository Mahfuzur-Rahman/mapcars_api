# Mapcars API — Developer Guide & File Map

> **What this file is:** a map of the backend. "Where does X live? If I want to
> change Y, which file do I edit?" Read this before touching the code.
> For accounts/keys to run it, see [`instruction.md`](instruction.md).
> For the schema, see [`database/README.md`](database/README.md).

---

## 1. The big picture (Clean Architecture / N-tier)

Four projects under `src/`. Dependencies point **inward** — inner layers never
know about outer ones.

```
        ┌─────────────────────────────────────────────┐
        │  Mapcars.Api          (HTTP / controllers)   │  ← outermost
        │     │ depends on                              │
        │     ▼                                         │
        │  Mapcars.Application  (business logic, DTOs)  │
        │     │ depends on                              │
        │     ▼                                         │
        │  Mapcars.Domain       (entities, enums)       │  ← innermost, depends on nothing
        └─────────────────────────────────────────────┘
                       ▲ implements interfaces
        ┌─────────────────────────────────────────────┐
        │  Mapcars.Infrastructure (EF, DB, JWT, email)  │  → references Application
        └─────────────────────────────────────────────┘
```

**Rule of thumb:**
- `Domain` = *what the business is* (a Rider, a Trip, their rules). No frameworks.
- `Application` = *what the app does* (use-cases), and the **interfaces** it needs.
- `Infrastructure` = *how* those interfaces are fulfilled (Postgres, JWT, Google…).
- `Api` = *how the outside world talks to it* (HTTP, JSON, auth).

---

## 2. Project-by-project — what each folder holds

### `src/Mapcars.Domain` — the core model (no dependencies)

| Folder / file | Contains | Edit when… |
|---------------|----------|------------|
| `Entities/*.cs` | The business objects: `Rider`, `Driver`, `Trip`, `Admin`, `Role`, `Menu`, `RoleMenu`, `AdminMenuPermission`, `VerificationCode` | You add a field to a concept, or a new concept |
| `Enums/*.cs` | `DriverStatus`, `TripStatus` | You add a new status/state |
| `Common/BaseEntity.cs` | Base class: `Id`, `CreatedAtUtc`, `UpdatedAtUtc` (timestamps auto-stamped on save) | Rarely — affects every entity |
| `Exceptions/DomainException.cs` | Thrown when a business rule is broken → becomes HTTP 400 | Rarely |

### `src/Mapcars.Application` — business logic + contracts

Organized **by feature** (`Riders/`, `Drivers/`, `Admins/`) plus a shared `Common/`.
Each feature folder follows the same shape:

| Sub-folder | Contains | Edit when… |
|------------|----------|------------|
| `<Feature>/Dtos/*` | Request & response shapes (what the API sends/receives). **All request models live here**, not in controllers | You change the API contract for that feature |
| `<Feature>/Interfaces/*` | Contracts: the service interface + the repository interface | You add a use-case or a data-access method |
| `<Feature>/Services/*` | **The business logic.** Orchestrates repos + unit of work | You change how a feature *behaves* |
| `<Feature>/Validators/*` | FluentValidation rules for that feature's request DTOs | You change input rules |
| `<Feature>/Mapping/*` | Entity → response DTO mapping helpers | You change response shape |

Shared bits:

| File | Contains |
|------|----------|
| `Common/Interfaces/*` | Cross-cutting contracts: `IGenericRepository<T>`, `IUnitOfWork`, `IPasswordHasher`, `IJwtService`, `IOtpService`, `IEmailService`, `ISmsService`, `IGoogleAuthService` |
| `Common/Dtos/AuthResponse.cs` | Shared `AuthResponse` + `OtpSentResponse` (used by rider & driver auth) |
| `Common/Exceptions/*` | `ValidationException`, `NotFoundException`, `UnauthorizedException` — the app's error vocabulary |
| `Common/Validation/CommonRules.cs` | **Reusable validation rules** (phone, email, OTP code, password). Use these so every feature validates the same field the same way |
| `DependencyInjection.cs` | `AddApplication()` — registers services + auto-registers **all** validators |

### `src/Mapcars.Infrastructure` — the "how" (DB, security, messaging)

| Folder / file | Contains | Edit when… |
|---------------|----------|------------|
| `Persistence/AppDbContext.cs` | EF Core context; lists every `DbSet`; auto-stamps timestamps; **is** the `IUnitOfWork` | You add a new table/entity |
| `Persistence/Configurations/*` | EF mapping per entity (table name, column names, indexes, max lengths). **This is where C# ↔ DB column mapping lives** | The DB schema changes |
| `Persistence/Repositories/*` | Data access: `GenericRepository<T>` (CRUD) + per-entity repos (`RiderRepository`, etc.) with custom queries | You add a query |
| `Security/JwtService.cs` | Issues JWT tokens | Token claims/expiry change |
| `Security/PasswordHasher.cs` | BCrypt hashing | Rarely |
| `Services/OtpService.cs` | Creates + verifies email/phone OTP codes | OTP logic changes |
| `Services/ConsoleEmailService.cs`, `ConsoleSmsService.cs` | **Dev stubs** that log instead of sending. Swap for real providers later | You integrate a real email/SMS provider |
| `Services/GoogleAuthService.cs` | Verifies Google ID tokens | Google sign-in changes |
| `DependencyInjection.cs` | `AddInfrastructure()` — wires DbContext + every interface→implementation | You add a new infra service/repo |

### `src/Mapcars.Api` — the HTTP edge

| File | Contains | Edit when… |
|------|----------|------------|
| `Program.cs` | App startup: CORS, JWT auth, the global validation filter, the HTTP pipeline, DI wiring | You add middleware, a policy, or startup config |
| `Controllers/*.cs` | Thin HTTP endpoints — translate HTTP ⇄ service calls. **No business logic here** | You add/rename a route |
| `Filters/ValidationActionFilter.cs` | Runs FluentValidation on every request automatically | Rarely |
| `Middleware/ExceptionHandlingMiddleware.cs` | Turns exceptions into consistent `problem+json` error responses | You add a new exception→status mapping |
| `appsettings.json` | Config **structure** (non-secret). Real secrets come from user-secrets | You add a config key |

### `database/` — the schema (source of truth)

Database-first: plain SQL scripts are the source of truth. **No EF migrations.**
See [`database/README.md`](database/README.md).

---

## 3. The lifecycle of a request (follow the data)

Example: `POST /api/v1/auth/riders/login`

```
HTTP request
   │
   ▼
[Api] RiderAuthController.Login(EmailLoginRequest)        ← Controllers/RiderAuthController.cs
   │   (ValidationActionFilter validates the DTO first)   ← Filters/ValidationActionFilter.cs
   ▼                                                          + Application/Riders/Validators/*
[Application] IRiderAuthService.LoginWithEmailAsync(...)   ← Riders/Services/RiderAuthService.cs
   │   business rules: account active? password matches?
   ▼
[Application] IRiderRepository.FindByEmailAsync(...)       ← interface in Riders/Interfaces/
   ▼
[Infrastructure] RiderRepository → AppDbContext → Postgres ← Persistence/Repositories/
   │
   ▼
returns Rider → service builds AuthResponse (JWT via IJwtService) → controller returns 200 JSON
```

If anything throws (`UnauthorizedException`, `ValidationException`, …),
`ExceptionHandlingMiddleware` converts it to the right HTTP status + JSON shape.

---

## 4. "I want to change X — where do I go?" (quick lookup)

| I want to… | Go to |
|------------|-------|
| Add a field to Rider/Driver/Trip | `Domain/Entities/*` → its `Persistence/Configurations/*` → the `database/*.sql` script |
| Add a brand-new table/concept | `Domain/Entities` → EF config + `DbSet` in `AppDbContext` → `database/*.sql` (see §6) |
| Change what the API returns | the feature's `Dtos/*` (+ its `Mapping/*`) |
| Change request input rules | the feature's `Validators/*` (reuse `Common/Validation/CommonRules`) |
| Change business behavior | the feature's `Services/*` |
| Add a new endpoint | a `Controllers/*.cs` (+ a service method on the interface) |
| Add a database query | the repository interface (`<Feature>/Interfaces`) + impl (`Persistence/Repositories`) |
| Change JWT claims / expiry | `Infrastructure/Security/JwtService.cs` (+ `Jwt:*` config) |
| Change OTP behavior (length, expiry) | `Infrastructure/Services/OtpService.cs` |
| Send real emails / SMS | replace `ConsoleEmailService` / `ConsoleSmsService` + re-register in `Infrastructure/DependencyInjection.cs` |
| Map a new exception to an HTTP status | `Api/Middleware/ExceptionHandlingMiddleware.cs` |
| Add CORS origin / change auth setup | `Api/Program.cs` |
| Register a new service/repo for DI | `Application/DependencyInjection.cs` (logic) **or** `Infrastructure/DependencyInjection.cs` (data/infra) |
| Change the DB schema | a `database/*.sql` script (then sync the EF config) |
| Add a config value / secret | `appsettings.json` (structure) + `dotnet user-secrets set` (real value) |

---

## 5. Conventions (please keep these consistent)

1. **Controllers are thin.** No business logic, no DB access — just call a service.
2. **Never expose entities.** The API speaks in DTOs; entities stay inside.
3. **Request DTOs live in `Application/<Feature>/Dtos`** — never as records inside a controller.
4. **Validation has one home:** every request DTO gets an `AbstractValidator<T>`; the
   global filter runs it automatically. Services do **business-rule** checks only
   (uniqueness, account state), not shape checks.
5. **Interfaces in `Application`, implementations in `Infrastructure`.** The Application
   layer must never reference EF Core, Npgsql, Stripe, etc. directly.
6. **One `SaveChangesAsync` per use-case**, called via `IUnitOfWork` at the end of a service method.
7. **Pass `CancellationToken ct` through** every async method.
8. **Database-first.** Edit `database/*.sql`; do **not** add EF migrations.

---

## 6. How to add a new feature (use Riders as the template)

1. **Domain:** add the entity in `Domain/Entities` (inherit `BaseEntity`); add enums if needed.
2. **Database:** add/extend a `database/*.sql` script to create the table.
3. **Application:**
   - `Dtos/` — request + response shapes
   - `Validators/` — an `AbstractValidator<T>` per request DTO
   - `Interfaces/` — the service interface + repository interface
   - `Services/` — the business logic
   - `Mapping/` — entity → response DTO
4. **Infrastructure:**
   - `Persistence/Configurations/` — EF mapping (table, columns, indexes)
   - add a `DbSet` to `AppDbContext`
   - `Persistence/Repositories/` — the repository implementation
5. **Api:** add a controller in `Controllers/`.
6. **Wire DI:** register the service in `Application/DependencyInjection.cs` and the
   repository in `Infrastructure/DependencyInjection.cs`. (Validators auto-register.)

That's the whole loop. Copy an existing **Riders** file as your starting point each time.
