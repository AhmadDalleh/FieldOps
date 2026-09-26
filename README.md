# FieldOps

Field service management for a single service company (HVAC, electrical, plumbing, maintenance): customers, sites, assets, work orders, dispatch, a technician mobile web app, inventory and invoicing.

- The plan lives in [`docs/`](docs/) and the working rules for contributors (and Claude Code) in [`CLAUDE.md`](CLAUDE.md).
- Progress is tracked in [`docs/06-roadmap.md`](docs/06-roadmap.md).

## Prerequisites

- .NET 10 SDK
- Node.js 24 (Angular 22 needs 22.22.3+ or 24.15+)
- Docker (for Postgres, Mailpit and the backend tests)

## Run locally

```bash
docker compose up -d                                   # postgres on :5432, mailpit on :8025
dotnet tool restore                                    # dotnet-ef
dotnet run --project src/FieldOps.Api                  # http://localhost:5000, Swagger at /swagger
cd web && npm install && npm start                     # http://localhost:4200 (proxies /api to :5000)
```

In Development the API applies migrations and seeds these users on start-up, all with the password `Pass123!`:
`admin@fieldops.local`, `dispatcher@fieldops.local`, `tech1@fieldops.local`, `tech2@fieldops.local`, `tech3@fieldops.local`.

## Test

```bash
dotnet test                 # domain, handler and API tests (Docker must be running)
cd web && npm test          # Vitest
```

## Configuration

Settings come from `appsettings.json` and environment variables:

| Variable | Purpose |
|---|---|
| `ConnectionStrings__Default` | PostgreSQL connection string |
| `Jwt__Key` | JWT signing key, at least 32 bytes. Required outside Development. |
| `Files__Root` | Folder for uploaded files |
| `Cors__Origins__0` | Allowed browser origin |

## Migrations

```bash
dotnet ef migrations add <Name> -p src/FieldOps.Infrastructure -s src/FieldOps.Api -o Persistence/Migrations
dotnet ef database update     -p src/FieldOps.Infrastructure -s src/FieldOps.Api
```
