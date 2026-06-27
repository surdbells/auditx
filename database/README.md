# database/

This directory is intentionally a thin pointer. AuditX uses **EF Core code-first**, so there are no
hand-written SQL migration scripts or seed files here — the authoritative artifacts live in the backend
Infrastructure project and run automatically on application startup.

## Where things actually live

| Concern | Location |
|---------|----------|
| **Schema migrations** | `backend/src/AuditX.Infrastructure/Persistence/Migrations/` (EF Core, code-first) |
| **Model snapshot** | `backend/src/AuditX.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs` |
| **Entity ↔ table mapping** | `backend/src/AuditX.Infrastructure/Persistence/Configurations/*.cs` (snake_case tables) |
| **Seed data** | `backend/src/AuditX.Infrastructure/Persistence/DbSeeder.cs` (idempotent; built-in roles + permission sets, bank settings, risk dimensions, entity-type taxonomy, notification default rules/templates, and — only when the Development identity provider is active — the seeded dev users) |
| **Append-only audit-trail trigger** | created inside the `InitialCreate` migration (rejects `UPDATE`/`DELETE` on `audit_trail`) |

## How it's applied

On startup the API calls `InitialiseDatabaseAsync` (in `Program.cs`), which runs `MigrateAsync()` to apply any
pending migrations and then `DbSeeder.SeedAsync(...)`. Recurring Hangfire jobs are registered afterwards.

## Working with migrations

```bash
# from backend/
dotnet ef migrations add <Name> \
  --project src/AuditX.Infrastructure --startup-project src/AuditX.Api \
  --output-dir Persistence/Migrations

dotnet ef database update \
  --project src/AuditX.Infrastructure --startup-project src/AuditX.Api
```

> Note: `dotnet ef migrations remove` and `migrations has-pending-model-changes` connect to / rebuild against
> the model — run them with a reachable SQL Server (or a fresh build), not `--no-build` against a stale assembly.
