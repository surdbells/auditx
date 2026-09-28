# AuditX Architecture

## 1. System overview

AuditX is a three-tier web application deployed single-tenant inside an institution's network:

- **Presentation** — Angular 21 SPA (standalone components, signals, Angular Material), served by Nginx.
- **Application/API** — ASP.NET Core 10 (controllers) exposing a REST API under `/api/v1`, a Hangfire
  scheduler for time-based jobs, and the notification/domain-event backbone.
- **Data** — SQL Server (transactional + configuration data) and a file-storage backend for evidence
  (later modules). Redis provides permission caching and the session-token denylist.

```
Browser ──HTTPS──> Nginx ──/api──> ASP.NET Core API ──> SQL Server
                                   │                  └─> Redis (cache, denylist)
                                   ├─> Active Directory (LDAPS / Kerberos)
                                   └─> Hangfire jobs (delegation expiry, …)
```

## 2. Backend folder structure (Clean Architecture)

Dependencies flow strictly inward: **API → Infrastructure → Application → Domain**.

```
backend/src/
  AuditX.Domain/          Entities, value objects, enums, domain events, business rules. No framework
                          or persistence dependencies. (Identity aggregates, AuditTrailEntry,
                          PermissionCatalogue, BuiltInRoles.)
  AuditX.Application/      Use cases: CQRS commands/queries + handlers, DTOs, FluentValidation validators,
                          ports (repository/identity/cache interfaces), the in-house dispatcher.
  AuditX.Infrastructure/  EF Core DbContext + configurations + migrations, repositories, the auditing
                          interceptor, AD/Dev identity providers, JWT service, Redis denylist, permission
                          resolver, seeder.
  AuditX.Api/             Controllers, RFC 7807 exception middleware, request-context/current-user,
                          JWT + Negotiate auth, [RequirePermission] filter, session-sliding middleware,
                          Hangfire wiring, Swagger, composition root (Program.cs).
backend/tests/            Domain (unit), Application (handler/unit), Infrastructure (Testcontainers SQL),
                          Api.IntegrationTests (Testcontainers SQL + Redis, full HTTP).
```

## 3. Key cross-cutting designs

- **CQRS dispatcher** — `IDispatcher` resolves `ICommandHandler<,>`/`IQueryHandler<,>` and runs a
  FluentValidation pipeline before handling. No third-party mediator (see ADR-0001).
- **API envelope** — success responses are `{ data, metadata: { request_id, … } }`; errors are RFC 7807
  `application/problem+json` with `error_code` and `field_errors[]` (validation → 422).
- **Audit trail (M11 core)** — every state change writes an `audit_trail` row through `IAuditRecorder`
  in the **same transaction** as the change. The table is **append-only**, enforced by a SQL trigger
  that rejects `UPDATE`/`DELETE`.
- **Auditing interceptor** — stamps `created/updated` columns and dispatches aggregate domain events
  after commit.
- **Soft delete** — `is_deleted` columns with a global EF query filter.

## 4. Database design (M1)

GUID (UUIDv7) primary keys, `DATETIMEOFFSET` (UTC) timestamps, snake_case table/column names, JSON
columns for flexible payloads. Tables: `users`, `roles`, `role_permissions`, `user_roles` (also models
delegations), `maker_checker_actions`, `maker_checker_gates`, `institution_settings`, `audit_trail`.

State machines and richer schemas for M2–M15 are introduced with their modules. Migrations are
code-first and live in `AuditX.Infrastructure/Persistence/Migrations`.

## 5. Authentication & authorization

- **Authentication** delegates to Active Directory: LDAPS for directory lookup, Kerberos/IWA for SSO,
  and a forms fallback that validates credentials with an LDAP bind. AuditX **stores no passwords**. A
  `Development` provider with seeded users allows the platform to run without a domain.
- **Session** — a signed (HMAC-SHA-256) JWT in an HttpOnly cookie, 8h sliding / 24h absolute lifetime,
  with a Redis denylist for logout/force-logout and AD-disablement detection on refresh.
- **Authorization** — AuditX-maintained roles and fine-grained, scopeable permissions, **independent of
  AD groups**. Effective permissions are the transitive union of role assignments, role inheritance
  (acyclic), and active delegations, cached in Redis. Enforced per request by `[RequirePermission]`.
- **Maker-checker** — gateable actions are captured as pending actions and require a different user to
  approve; approval replays the action atomically. Self-approval is always forbidden.

## 6. Security design

TLS 1.2+ at the proxy; HttpOnly/Secure/SameSite cookies; RFC 7807 errors without resource-existence
leaks; least-privilege provisioning (no default roles); append-only, transactional audit trail; secrets
via configuration/secret store; OWASP-aligned headers at Nginx; integration credentials encrypted at
rest (DataProtection). No required outbound internet egress from the deployment.

## 7. Operations

Health at `/admin/health` (DB + Redis); structured JSON logs via Serilog; Hangfire for scheduled jobs
(e.g. hourly delegation expiry). Backups, SIEM export, and the ITANDT support channel are delivered with
M11/M14/M15.
