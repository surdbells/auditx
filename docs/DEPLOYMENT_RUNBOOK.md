# AuditX Enterprise — Deployment Runbook

_On-premises internal-audit platform for banks. ASP.NET Core 10 API + Angular 21 SPA + SQL Server + Redis._

This runbook covers three target environments:

1. **[Local Docker test environment](#1-local-docker-test-environment)** — one-command demo / QA stack.
2. **[Microsoft Azure](#2-microsoft-azure)** — managed PaaS reference deployment.
3. **[Nigerian-bank on-premises Windows Server](#3-nigerian-bank-on-premises-windows-server)** — the primary
   production target (Active Directory, SQL Server, IIS/Kestrel, no internet egress).

It assumes the repository layout: `backend/` (the .NET solution `AuditX.slnx`), `frontend/auditx-web/` (the Angular
app), `docker/` (Dockerfiles + nginx), `docker-compose.yml`, and `backend/src/AuditX.Infrastructure/Persistence/Migrations/`
(EF Core code-first migrations + `DbSeeder`).

---

## 0. Architecture & runtime dependencies

| Component | Production requirement | Purpose |
|---|---|---|
| **API** | .NET 10 runtime (ASP.NET Core), 2+ instances behind a load balancer | REST API (`/api/v1`), Hangfire recurring jobs |
| **SPA** | Static files served by nginx / IIS | Angular UI; calls the API; never holds the JWT (HttpOnly cookie) |
| **SQL Server** | 2019+ (2022 recommended), TLS on | All data + the Hangfire job store + the append-only audit trail (SQL trigger) |
| **Redis** | 6+ | Permission cache + session-token denylist |
| **Active Directory** | LDAPS (636) + Kerberos/IWA | Authentication (no passwords are stored by AuditX) |
| **SMTP** | Relay host | M10 email notifications |
| **Shared file storage** | Path/volume | Evidence files + generated reports/dossiers/AC packs (SHA-256 sealed) |
| **DataProtection key ring** | Shared, persisted, backed-up | Decrypts M14 integration credentials across instances/restarts |

### Configuration keys (override per environment via env vars or a secret store)

Environment-variable form uses `__` for nested keys (e.g. `ConnectionStrings__Default`).

| Key | Required | Notes |
|---|---|---|
| `ConnectionStrings__Default` | ✅ | SQL Server connection string. Use `Encrypt=True` + a trusted cert in production. |
| `Identity__Provider` | ✅ | `ActiveDirectory` (LDAPS) or `ActiveDirectoryApi` (the bank's AD REST gateway — see [identity-ad-rest-contract.md](identity-ad-rest-contract.md)) in production. `Development` is **rejected outside the Development environment** by the startup guard. |
| `Jwt__SigningKey` | ✅ | ≥ 32 bytes, high-entropy, from a secret store. The startup guard rejects empty / `CHANGE-ME…` / < 32 bytes outside Development. |
| `Jwt__Issuer`, `Jwt__Audience` | ⬜ | Default `auditx`. |
| `Redis__ConnectionString` | ✅ | e.g. `redis-host:6379`. |
| `ActiveDirectory__Host` / `Port` / `UseLdaps` / `BaseDn` / `ServiceAccountDn` / `ServiceAccountPassword` / `UpnSuffix` | ✅ (LDAPS) | LDAPS service account for user lookup (`Provider=ActiveDirectory`). |
| `ActiveDirectoryApi__BaseUrl` / `ApiKey` (+ optional `AuthenticatePath` / `LookupPath` / `StatusPath` / `ApiKeyHeader` / `TimeoutSeconds`) | ✅ (AD-REST) | The bank's AD REST gateway (`Provider=ActiveDirectoryApi`). See [identity-ad-rest-contract.md](identity-ad-rest-contract.md). |
| `DataProtection__KeyRingPath` | ✅ (multi-instance / container) | Shared, persisted folder for the key ring. On a single Windows host the default profile/registry store is fine. |
| `Cors__Origins__0` … | ✅ | The SPA origin(s), e.g. `https://auditx.bank.internal`. |
| `Database__MigrateOnStartup` | ⬜ | `true` applies EF migrations on boot. Prefer `false` in production + a controlled migration step (below). |
| `Storage__EvidenceRoot` | ✅ | Path/volume for evidence + artefacts (verify it is on backed-up storage). |
| `AllowedHosts` | ⬜ | Set to the API host name in production. |

### Startup safety guard (fail-fast)

Outside the `Development` environment the API **refuses to start** if it would run insecurely:
- `Identity:Provider = Development` (the seeded well-known accounts), or
- `Jwt:SigningKey` missing / the committed placeholder / shorter than 32 bytes, or
- `ConnectionStrings:Default` empty.

This is intentional — a misconfigured production deployment fails loudly instead of silently exposing seeded admin
accounts or forgeable tokens. Supply the real values before starting.

---

## 1. Local Docker test environment

The fastest way to stand up the whole stack for QA/demo. Uses the **seeded Development identity provider** (no domain
required) and runs the API container in the `Development` environment.

### Prerequisites
- Docker Desktop (or Docker Engine + Compose v2).
- ~4 GB free RAM for the SQL Server container.

### Run
```bash
# from the repository root
docker compose up -d --build
docker compose ps          # wait for sqlserver/redis healthy, api + web up
docker compose logs -f api # watch migrations apply + seed run
```

Services:
- API → http://localhost:8080 (Swagger UI at http://localhost:8080/swagger)
- SPA → http://localhost:4200
- SQL Server → localhost:1433 (`sa` / `Auditx_Local_Dev_123`), Redis → localhost:6379, RabbitMQ mgmt → 15672

### Seeded development users (Development provider only)
| Username | Role | Password |
|---|---|---|
| `admin` | AuditX Administrator | `Passw0rd!` |
| `manager` | _(no role — grant as needed)_ | `Passw0rd!` |
| `auditor` | _(no role)_ | `Passw0rd!` |
| `auditee` | _(no role)_ | `Passw0rd!` |

> The compose stack sets `ASPNETCORE_ENVIRONMENT=Development` precisely because the seeded provider is blocked in
> non-Development environments by the safety guard. **Do not point this compose file at a production database.**

### Smoke test
```bash
curl -s http://localhost:8080/swagger/v1/swagger.json | head -c 200      # OpenAPI doc generates
curl -s -c jar.txt -X POST http://localhost:8080/api/v1/auth/login \
  -H "Content-Type: application/json" -d '{"username":"admin","password":"Passw0rd!"}'
curl -s -b jar.txt http://localhost:8080/api/v1/users/me                  # 200 + the admin profile
```

### Tear down
```bash
docker compose down            # keep data
docker compose down -v         # also drop the SQL/Redis volumes (fresh DB next time)
```

### Automated tests (CI parity)
```bash
# Backend (Docker-gated integration tests spin their own Testcontainers — Docker must be running)
cd backend && dotnet test AuditX.slnx
# Frontend
cd frontend/auditx-web && npm ci && npm run test:ci && npm run build
```
The same steps run in `.github/workflows/ci.yml`.

---

## 2. Microsoft Azure

A managed reference topology. Adjust to the bank's landing-zone standards (private endpoints, VNet, Front Door).

### Recommended services
| Concern | Azure service |
|---|---|
| API | **App Service (Linux, .NET 10)** or Container Apps — 2+ instances |
| SPA | **Static Web Apps** or an App Service serving the built `dist/` |
| Database | **Azure SQL Database** (or SQL MI for full T-SQL/Agent parity) |
| Cache/denylist | **Azure Cache for Redis** |
| Secrets | **Azure Key Vault** (referenced from App Service config) |
| DataProtection key ring | **Blob storage + Key Vault** (`PersistKeysToAzureBlobStorage` + `ProtectKeysWithAzureKeyVault`) or a mounted share via `DataProtection:KeyRingPath` |
| Identity | the bank's AD via **VPN/ExpressRoute** to a reachable domain controller (LDAPS 636), or Entra Domain Services |
| Files | **Azure Files** share mounted at `Storage:EvidenceRoot` |
| TLS / WAF | **Application Gateway / Front Door** |

### Steps
1. **Provision** SQL DB, Redis, Key Vault, Storage, the App Services (IaC: Bicep/Terraform per the landing zone).
2. **Secrets → Key Vault**: `Jwt--SigningKey`, the SQL connection string, the AD service-account password. Reference
   them from App Service application settings as `@Microsoft.KeyVault(SecretUri=…)`.
3. **App Service config** (application settings):
   - `ASPNETCORE_ENVIRONMENT=Production`
   - `ConnectionStrings__Default` (Azure SQL; `Encrypt=True`)
   - `Identity__Provider=ActiveDirectory` + the `ActiveDirectory__*` keys
   - `Redis__ConnectionString`
   - `DataProtection__KeyRingPath` (mounted Azure Files) **or** wire blob+Key Vault DataProtection
   - `Cors__Origins__0=https://<spa-host>`
   - `Storage__EvidenceRoot` (mounted Azure Files)
   - `Database__MigrateOnStartup=false`
4. **Build & publish**:
   ```bash
   dotnet publish backend/src/AuditX.Api/AuditX.Api.csproj -c Release -o ./publish
   # deploy ./publish via az webapp deploy / zip deploy / container image
   cd frontend/auditx-web && npm ci && npm run build   # deploy dist/ to Static Web Apps / App Service
   ```
5. **Apply migrations** (controlled — see §4) from a pipeline step or a jump host.
6. **Networking**: private endpoints for SQL/Redis/Storage/Key Vault; App Gateway/Front Door terminates TLS;
   restrict the SPA origin via CORS; managed identity for Key Vault access.
7. **Validate** with the §5 smoke tests against the public host.

> Scale-out note: because 2+ API instances share the DataProtection key ring and the Redis denylist, sessions and
> M14 credential decryption work across instances. Hangfire recurring jobs are safe under multiple instances (SQL
> Server storage coordinates them).

---

## 3. Nigerian-bank on-premises Windows Server

The primary production target: a domain-joined Windows Server farm with **no internet egress**, the bank's
**Active Directory**, and an on-prem **SQL Server**.

### 3.1 Topology
- 2× **Windows Server 2022** app hosts (domain-joined), behind a hardware/F5 load balancer (sticky not required).
- 1× **SQL Server 2019/2022** (clustered/AG for HA), TLS enabled.
- 1× **Redis** (Redis on Linux, Memurai, or Redis Enterprise on Windows).
- The bank's **Active Directory** domain controllers reachable over **LDAPS (636)** + Kerberos.
- An internal **SMTP relay**.
- A backed-up **shared file share** (UNC/SMB) for evidence + artefacts + the DataProtection key ring.
- TLS server certificate from the bank's internal CA for the API host name.

### 3.2 Prerequisites on each app host
- **ASP.NET Core 10 Runtime (Hosting Bundle)** + **IIS** with the ASP.NET Core Module v2 (reverse proxy to Kestrel),
  or run Kestrel as a Windows Service behind the LB. (Air-gapped: stage the Hosting Bundle installer internally.)
- A domain **gMSA or service account** for the API app pool (used for Kerberos/IWA SSO).
- Read/write to the shared file share for `Storage:EvidenceRoot` and `DataProtection:KeyRingPath`.

### 3.3 Database
1. Create the `auditx` database; create a **least-privilege SQL login** (or use Windows-auth with the app-pool
   identity) — `db_owner` for the first migration, then `db_datareader`/`db_datawriter` + execute for runtime.
2. Apply migrations (§4). This also creates the **append-only `audit_trail` trigger** and the Hangfire schema.
3. Configure SQL Server for **TLS** (force encryption); the API connection string uses `Encrypt=True` with a trusted cert.

### 3.4 Active Directory
- Set `Identity__Provider=ActiveDirectory` and the `ActiveDirectory__*` keys: `Host` (a DC / LDAPS VIP), `Port=636`,
  `UseLdaps=true`, `BaseDn`, `ServiceAccountDn` + `ServiceAccountPassword` (LDAPS lookup), `UpnSuffix` (e.g. `bank.local`).
- For **Kerberos/IWA SSO**, the API app pool runs as the gMSA and the SPN is registered for the API host name. The
  forms-fallback login validates AD credentials by an LDAP bind. **AuditX stores no passwords.**
- Map the bank's audit personnel to AuditX roles after first sign-in (JIT provisioning creates the user in
  `awaiting_role_assignment`; an Administrator grants roles). The custom **AC Member / AC Chair / Chief Internal
  Auditor** roles are seeded and can be granted to committee members.

### 3.5 Secrets & config (per host)
Store config outside the web root — use machine environment variables, `appsettings.Production.json` with restricted
ACLs, or a secret manager (e.g. CyberArk). Required: `Jwt__SigningKey` (≥32 B from the secret store),
`ConnectionStrings__Default`, the `ActiveDirectory__*` keys, `Redis__ConnectionString`, `Cors__Origins__0`
(the SPA URL), `Storage__EvidenceRoot` (UNC), `DataProtection__KeyRingPath` (UNC, shared by both hosts),
`AllowedHosts` (the API host), `ASPNETCORE_ENVIRONMENT=Production`, `Database__MigrateOnStartup=false`.

> **Both app hosts must point `DataProtection:KeyRingPath` at the SAME shared, backed-up folder** so M14 integration
> credentials encrypted on one host decrypt on the other (and survive restarts).

### 3.6 Deploy the API
```powershell
# On a build/jump host with the SDK (air-gapped: build internally, copy the artefact)
dotnet publish backend\src\AuditX.Api\AuditX.Api.csproj -c Release -o .\publish
# Copy .\publish to each app host, e.g. C:\inetpub\auditx-api, then in IIS:
#  - create an app pool (No Managed Code) running as the gMSA
#  - create a site bound to https with the internal TLS cert
#  - the ASP.NET Core Module hosts Kestrel (web.config is emitted by publish)
# Recycle the app pool; confirm the startup safety guard passed (see logs).
```

### 3.7 Deploy the SPA
```powershell
cd frontend\auditx-web
npm ci
npm run build          # outputs dist\auditx-web\
# Serve dist\ from IIS (static site) or nginx. Point the SPA's API base URL at the API host
# (src\environments\environment.ts) BEFORE building, or via a runtime config file.
```
Serve the SPA over HTTPS; set the API `Cors:Origins` to the SPA origin.

### 3.8 Hardening on Windows Server
- TLS 1.2/1.3 only; HTTP→HTTPS redirect at the LB/IIS (the API also issues HSTS + security headers outside Development).
- Restrict inbound to 443; the API↔SQL/Redis/AD/SMTP traffic stays on the internal network.
- Run the app pool as a least-privilege gMSA; ACL the publish folder, config, key-ring, and evidence share.
- Schedule SQL backups (incl. the audit trail) + key-ring backup; test restores (M15 restore-drill records this).
- Forward Serilog output to the bank SIEM (M14 SIEM export seam) and the Windows Event Log.

---

## 4. Database migrations (controlled)

EF Core code-first. Two options:

**A. On-startup (simple, lower environments):** `Database__MigrateOnStartup=true` applies pending migrations as the
API boots (used by the Docker stack).

**B. Controlled (production-recommended):** keep `MigrateOnStartup=false` and apply migrations as an explicit,
reviewed step from a host with the SDK + DB access:
```bash
cd backend
dotnet ef database update --project src/AuditX.Infrastructure --startup-project src/AuditX.Api
# Air-gapped alternative — generate an idempotent SQL script for a DBA to review + run:
dotnet ef migrations script --idempotent --project src/AuditX.Infrastructure --startup-project src/AuditX.Api -o auditx-migrations.sql
```
Migrations are additive and ordered; the latest is `ProductionHardening`. The seed (`DbSeeder`) runs on startup and is
idempotent (built-in roles/permissions, default dashboards, notification rules/templates, AC + CIA roles, default grid
and report/exception-defaults config).

---

## 5. Post-deploy smoke test (all environments)

```bash
# 1. App is up + OpenAPI generates
curl -fsS https://<api-host>/swagger/v1/swagger.json > /dev/null && echo "api up"

# 2. Auth (AD in prod; dev users locally) — expect a Set-Cookie: auditx.session
curl -fsS -c jar.txt -X POST https://<api-host>/api/v1/auth/login \
  -H "Content-Type: application/json" -d '{"username":"<user>","password":"<pass>"}'

# 3. Authenticated call
curl -fsS -b jar.txt https://<api-host>/api/v1/users/me

# 4. Permission gate returns 403 (not 500) for an unauthorized action; 401 when unauthenticated
curl -s -o /dev/null -w "%{http_code}\n" https://<api-host>/api/v1/audits   # 401

# 5. Security headers present
curl -sI https://<api-host>/api/v1/users/me | grep -iE "strict-transport-security|x-content-type-options|content-security-policy"

# 6. Background jobs registered — open the Hangfire dashboard / check logs for the recurring jobs.
```
Functional acceptance: sign in as an Administrator, grant a role, create an audit, run the checklist, raise an
exception + MAP, generate a report (download + verify-hash), open a dashboard, and generate/approve/distribute an AC
pack. Confirm `audit_trail` rows exist and a direct `UPDATE`/`DELETE` on `audit_trail` is rejected by the trigger.

---

## 6. Rollback

- **App:** keep the previous published artefact (or container image tag); repoint IIS/App Service/compose to the prior
  version and recycle. The API is stateless.
- **Database:** migrations are forward-only by policy. For a bad migration, restore from the pre-deploy backup taken in
  the change window, or apply a corrective migration. Always snapshot the DB immediately before applying migrations in
  production.
- **Config/secret rotation:** rotating `Jwt:SigningKey` invalidates all live sessions (users re-authenticate) — expected.
- **DataProtection key ring:** never delete it — losing it makes stored M14 integration credentials undecryptable
  (they must be re-entered). Back it up with the database.

---

## 7. Operational notes

- **Recurring jobs (Hangfire, SQL-backed):** delegation expiry (hourly), webhook retry (5 min), audit auto-start
  (hourly), notification retry (1 min), recurrence-cluster scan (daily), AC-pack generation (quarterly). Safe under
  multiple API instances.
- **Caches:** permission cache + session denylist in Redis; the active-config cache (M12) is in-memory with a short TTL
  and event invalidation. A Redis outage degrades authenticated requests — keep Redis HA.
- **Scaling:** scale the API horizontally; SQL Server is the state of record. Ensure the shared DataProtection key ring
  + evidence share are reachable by every instance.
- **Deferred operational features** (documented per-module): native PDF rendering (HTML+DOCX ship today), the M13
  external-NED token auth scheme (AC members use AD + the seeded role), notification digests/quiet-hours, audit-trail
  retention enforcement, SIEM streaming transport, HRIS transmission — see the `mN_blueprint.md` files.
