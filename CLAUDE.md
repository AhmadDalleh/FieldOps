# CLAUDE.md — FieldOps (Field Service Management)

## What this is
FieldOps is a web app that a service company (HVAC, electrical, plumbing, maintenance, etc.) uses to manage customers, their sites, and their equipment. It also covers work orders, scheduling and dispatch, technician field work on a mobile web app, spare parts, and invoicing.
It serves a single company and is not multi-tenant. It has three roles: **Admin**, **Dispatcher**, and **Technician**.

The full plan lives in `docs/`. **Read the relevant doc before touching a feature.**

| Doc | Use it for |
|---|---|
| `docs/01-overview.md` | scope, personas, MVP vs later |
| `docs/02-architecture.md` | layers, folders, auth, real-time, request flow |
| `docs/03-database.md` | ERD, tables, enums, indexes |
| `docs/04-user-stories.md` | **source of truth** for behavior and acceptance criteria |
| `docs/05-api.md` | endpoints and contracts |
| `docs/06-roadmap.md` | build order and progress checkboxes |
| `docs/07-flows.md` | work-order state machine, sequence diagrams, screens |
| `docs/08-testing.md` | test strategy and examples |

## Stack
- **Backend:** .NET 10, ASP.NET Core Minimal APIs, EF Core 10 + Npgsql, PostgreSQL 17, FluentValidation, ASP.NET Core Identity + JWT (access + refresh), SignalR, QuestPDF (invoice PDFs), Serilog.
- **Frontend:** Angular (current stable), standalone components, signals, Angular Material, CDK drag-drop (dispatch board), Leaflet + OpenStreetMap, PWA (technician app).
- **Tests:** xUnit, Shouldly, Testcontainers (PostgreSQL), WebApplicationFactory, NSubstitute (external boundaries only), Vitest (Angular), Playwright (a few E2E flows).
- **Dev infra:** docker-compose with `postgres` and `mailpit` (a fake SMTP server with a web UI on :8025).

## Repo layout
```
src/
  FieldOps.Domain/          entities, enums, domain rules. No package dependencies.
  FieldOps.Application/     Features/<Feature>/<UseCase>.cs (command/query + validator + handler), DTOs, interfaces
  FieldOps.Infrastructure/  AppDbContext, EF configurations, migrations, email, file storage, PDF, identity
  FieldOps.Api/             Program.cs, Endpoints/, Hubs/, auth policies, ProblemDetails mapping
tests/
  FieldOps.Domain.Tests/        pure unit tests (state machine, calculations)
  FieldOps.Application.Tests/   handlers against real Postgres (Testcontainers)
  FieldOps.Api.Tests/           HTTP-level tests incl. authorization
web/                          Angular app (office UI + /tech mobile routes)
docs/
docker-compose.yml
```

## Commands
```bash
docker compose up -d                                   # postgres + mailpit
dotnet build
dotnet test
dotnet run --project src/FieldOps.Api                  # API + Swagger at /swagger
dotnet ef migrations add <Name> -p src/FieldOps.Infrastructure -s src/FieldOps.Api -o Persistence/Migrations
dotnet ef database update     -p src/FieldOps.Infrastructure -s src/FieldOps.Api
cd web && npm install && npm start                     # http://localhost:4200 (proxy /api -> API)
cd web && npm test                                     # Vitest
cd web && npx playwright test                          # E2E (needs API + web running)
```
Seeded dev users (created by `DevSeeder` in Development only) are `admin@fieldops.local`, `dispatcher@fieldops.local`, and `tech1..tech3@fieldops.local`. All of them use the password `Pass123!`.

## Architecture rules
- Dependencies point inward. The API depends on Application, Application depends on Domain, and Infrastructure implements the interfaces defined in Application.
- **One file per use case:** `Application/Features/WorkOrders/CreateWorkOrder.cs` holds `record CreateWorkOrderCommand`, `CreateWorkOrderValidator`, and `CreateWorkOrderHandler`.
- Handlers are plain classes implementing `ICommandHandler<TCmd, TResult>` or `IQueryHandler<TQuery, TResult>`. They are registered by an assembly scan in `Application/DependencyInjection.cs`. **Do not use MediatR.**
- Handlers use `IAppDbContext` directly. **There is no repository or unit-of-work layer on top of EF.**
- Queries project to DTOs with `.AsNoTracking().Select(...)`. Mapping is written by hand, **with no AutoMapper**.
- Invariants live in entities. For example, `workOrder.Start(technicianId, now)` throws or returns an error if the transition is illegal. Handlers orchestrate the work and do not contain business rules.
- Expected failures return `Result` or `Result<T>` with an `Error` of type NotFound, Validation, Conflict, or Forbidden. Throw only for bugs and infrastructure faults. Endpoints map `Result` to `ProblemDetails` using one shared helper.
- Endpoints stay thin: bind the request, call the handler, map the result. Each feature gets one file, `Api/Endpoints/<Feature>Endpoints.cs`, using `MapGroup`.
- Authorization uses three policies: `AdminOnly`, `OfficeStaff` (Admin or Dispatcher), and `TechnicianOnly`. **Technicians may only read or act on work orders assigned to them.** Enforce this in the handler through `ICurrentUser`.
- Time comes from `TimeProvider`, which is injected so tests can control it. Never call `DateTime.Now`.

## Conventions
- IDs are `Guid` created with `Guid.CreateVersion7()`. Human-readable numbers such as `WO-000123` and `INV-000045` come from the `number_sequences` table.
- The database uses snake_case names (via `EFCore.NamingConventions`). Timestamps are `timestamptz` holding UTC values (`DateTimeOffset`). Money is `numeric(18,2)` (`decimal`).
- The frontend displays times in the `Asia/Dubai` timezone. The API always sends and receives UTC ISO-8601.
- Optimistic concurrency uses the Postgres `xmin` column on `WorkOrder`, `Invoice`, and `StockLevel`. A conflict returns 409.
- Audit fields (`CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`) are set by a `SaveChanges` interceptor.
- Master data (Customer, Site, Asset, Part, Technician) is soft-deleted with `IsActive = false`. Never hard-delete anything referenced by a work order.
- C# style: file-scoped namespaces, nullable enabled, records for commands, queries, and DTOs, `CancellationToken` passed everywhere, and `async` all the way down.
- List endpoints accept `page`, `pageSize` (max 100), `search`, and `sort`, and return `PagedResult<T> { items, page, pageSize, totalCount }`.
- Angular style: standalone components, `inject()`, signals for state, `ChangeDetectionStrategy.OnPush`, typed reactive forms, and one `*.api.ts` service per feature. **Do not use NgRx.**
- API routes follow `/api/{plural-kebab-resource}`. Actions that are not CRUD are POST sub-routes, for example `POST /api/work-orders/{id}/start`.

## How to build a feature (always follow this loop)
1. Find the story in `docs/04-user-stories.md` and its phase in `docs/06-roadmap.md`.
2. Check the tables in `docs/03-database.md`. Add or update the entity and its configuration, then create a migration if the schema changed.
3. **Write failing tests first** from the acceptance criteria: domain tests for rules, and Application or API tests for behavior and authorization.
4. Implement the backend in this order: entity method, then handler and validator, then endpoint.
5. Run `dotnet test` until everything is green.
6. Build the Angular screen. Add Vitest tests for any non-trivial component or service logic.
7. Tick the story checkbox in `docs/06-roadmap.md`. Summarize what changed, what was tested, and any deviations from the docs.

Keep each change scoped to the current story. If a story is ambiguous or conflicts with the docs, **ask before inventing scope**.

## Do NOT (YAGNI)
- Microservices, message brokers, event sourcing, separate read databases, Redis, or Kubernetes.
- Generic repositories, the specification pattern, AutoMapper, MediatR, or pipeline behaviours.
- Multi-tenancy, offline sync, or a customer portal. These are in "Later" in the roadmap.
- New NuGet or npm packages without a one-line justification in your summary.

## Testing rules
- Every acceptance criterion needs at least one test.
- Application and API tests use a real Postgres through Testcontainers. **Never mock `DbContext`.**
- Mock only external boundaries: `IEmailSender`, `IFileStorage`, and `TimeProvider` (use `FakeTimeProvider`).
- Every endpoint gets an authorization test covering the allowed roles and at least one forbidden role.
- Test names are plain sentences, for example `Start_fails_when_work_order_is_not_dispatched`.
