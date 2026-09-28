# AuditX Enterprise — On-Premises Edition v2.0

Enterprise internal-audit platform for an institution's Internal Audit function, deployed single-tenant inside
the institution's network. Built to the AuditX BRD/PRD/User-Story Catalogue v2.0 and the Claude Code Project
Standards (ASP.NET Core 10 + Angular, Clean Architecture).

> **Status:** Full platform delivered end-to-end across all modules — identity, universe & planning,
> templates, engagements & fieldwork, findings & remediation, controls & compliance, sanctions, reporting,
> analytics, audit committee, and notifications.
>
> **Documentation:** [docs/MODULES.md](docs/MODULES.md) (module & submodule map) ·
> [docs/PROJECT_DOCUMENTATION.md](docs/PROJECT_DOCUMENTATION.md) (architecture, setup, operations).

## What's implemented

- **Clean Architecture .NET 10 solution** — Domain / Application / Infrastructure / API, with a
  hand-rolled CQRS dispatcher, FluentValidation, Serilog, EF Core (code-first, GUID keys), Redis,
  Hangfire, and an append-only audit trail enforced by a database trigger.
- **M1 Identity**: Active Directory authentication (LDAPS lookup + Kerberos/IWA SSO + forms fallback),
  no stored passwords, JWT session cookies (8h sliding / 24h absolute, Redis denylist), just-in-time
  provisioning, AuditX-maintained roles/permissions independent of AD groups, scoped permissions with
  role inheritance (cycle-prevented), configurable maker-checker dual control, and time-bounded
  delegation with an hourly expiry job.
- **Angular 21 SPA** (standalone, signals, Angular Material) — login/SSO, awaiting-role, and the admin
  identity screens (users, roles, maker-checker queue, delegations).

## Tech stack

| Area | Technology |
|------|------------|
| Backend | .NET 10, ASP.NET Core 10 (controllers), EF Core 10, SQL Server 2022, Redis 7, Hangfire, Serilog, FluentValidation |
| Auth | Active Directory (LDAPS + Kerberos); JWT session tokens; `Development` provider for local use |
| Frontend | Angular 21, Angular Material, SCSS, RxJS/Signals, PWA |
| Infra | Docker / Docker Compose, Nginx, GitHub Actions |

## Repository layout

```
backend/    ASP.NET Core solution (src/ + tests/)   — see architecture.md
frontend/   Angular workspace (auditx-web)
database/   SQL helper scripts (EF migrations live in backend/src/AuditX.Infrastructure/Persistence/Migrations)
docker/     Dockerfiles + nginx config
docs/       architecture.md, ADRs
.github/    CI workflows
```

## Run locally

### Option A — Docker Compose (everything)

```bash
docker compose up --build
```

- API: http://localhost:8080 (Swagger in Development at `/swagger`, health at `/admin/health`)
- Web: http://localhost:4200
- The API auto-applies migrations and seeds built-in roles, default maker-checker gates, and
  development users on startup.

### Option B — Run services on the host

1. Start dependencies: `docker compose up -d sqlserver redis`
2. Backend:
   ```bash
   cd backend
   dotnet tool restore
   dotnet run --project src/AuditX.Api      # auto-migrates + seeds in Development; Swagger at /swagger
   ```
3. Frontend:
   ```bash
   cd frontend/auditx-web
   npm ci
   npm start                                # ng serve with proxy to the API (proxy.conf.json)
   ```

### Development sign-in

When `Identity:Provider = Development`, four seeded users are available (password **`Passw0rd!`**):

| Username | Role |
|----------|------|
| `admin` | AuditX Administrator (bootstrapped) |
| `manager` | none (awaiting role assignment) |
| `auditor` | none |
| `auditee` | none |

Sign in as `admin` to grant roles to the others.

## Configuration

Configured via `backend/src/AuditX.Api/appsettings*.json` or environment variables (double-underscore
form, e.g. `ConnectionStrings__Default`):

| Key | Purpose |
|-----|---------|
| `ConnectionStrings:Default` | SQL Server connection string |
| `Redis:ConnectionString` | Redis endpoint |
| `Identity:Provider` | `ActiveDirectory` (production) or `Development` |
| `ActiveDirectory:*` | LDAPS host/port, base DN, service account |
| `Jwt:SigningKey` | HMAC-SHA-256 signing key (**override in every deployment**, ≥ 32 bytes) |
| `Cors:Origins` | Allowed SPA origins |
| `Database:MigrateOnStartup` | Apply migrations + seed at startup (default in Development) |

## Tests

```bash
# Backend (unit tests run anywhere; integration tests use Testcontainers and require Docker)
cd backend && dotnet test

# Frontend (requires Chrome)
cd frontend/auditx-web && npm test -- --watch=false --browsers=ChromeHeadless
```

## Deployment

Production deploys behind the institution's reverse proxy. Build the container images
(`docker/Api.Dockerfile`, `docker/Web.Dockerfile`), set `Identity:Provider=ActiveDirectory`, supply the
AD/SMTP/SIEM configuration, and apply migrations through the controlled release process. See
[architecture.md](architecture.md) for topology and security design.

---

© ITANDT Solutions Ltd. Confidential.
