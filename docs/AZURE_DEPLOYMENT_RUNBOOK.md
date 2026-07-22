# AuditX — Azure Deployment Runbook

_Detailed companion to [DEPLOYMENT_RUNBOOK.md](DEPLOYMENT_RUNBOOK.md) §2. That file covers three environments in
overview; this one is the step-by-step, copy-pasteable walkthrough for Microsoft Azure specifically — IaC, CI/CD,
migrations, smoke tests, rollback._

Target audience: whoever runs the first Azure deployment (or reviews the pipeline). Assumes an Azure subscription,
`az` CLI ≥ 2.60, and Owner/Contributor rights on the target resource group.

---

## 1. Architecture

```
                                   ┌─────────────────────────────┐
 Internet ──HTTPS──▶ Container Apps│  auditx-web (nginx + SPA)   │
                     external      │  external ingress, port 80  │
                     ingress       └──────────────┬──────────────┘
                                                   │ same-origin proxy
                                                   │ /api/*  → http://auditx-api
                                                   │ (nginx, cookie stays same-site)
                                                   ▼
                                   ┌─────────────────────────────┐
                                   │  auditx-api (ASP.NET Core)  │
                                   │  internal-only ingress:8080 │
                                   └───┬──────┬──────┬───────┬───┘
                                       │      │      │       │
                              Key Vault│      │Redis │  Azure│Files (x2)
                              (secrets)│      │Cache │  evidence + keyring
                                       │      │      │       │
                                       ▼      ▼      ▼       ▼
                                 (managed  Azure   Azure   mounted
                                  identity) SQL    Cache   volumes
                                                            │
                              VPN/ExpressRoute ──▶ bank Active Directory (LDAPS 636)
```

Both container images already exist in this repo unmodified for this purpose — `docker/Api.Dockerfile` and
`docker/Web.Dockerfile` (see [docker-compose.yml](../docker-compose.yml) for the local equivalent). The only
change made to support Azure was templating the nginx upstream: `docker/nginx.conf.template` now proxies to
`http://${API_UPSTREAM}` (substituted by nginx's built-in envsubst-on-start), defaulting to `api:8080` so Compose
and the on-prem IIS/nginx path in [DEPLOYMENT_RUNBOOK.md §3](DEPLOYMENT_RUNBOOK.md#3-nigerian-bank-on-premises-windows-server)
are unaffected. Azure Container Apps sets `API_UPSTREAM=auditx-api` (the API app's name — Container Apps' internal
DNS resolves same-environment apps by name, with no port).

**Why same-origin, not split hostnames:** AuditX's session is an HttpOnly cookie (`auditx.session`), not a bearer
token the SPA attaches itself. Two Static-Web-App-style separate origins would need `SameSite=None` cross-site
cookies plus CORS-with-credentials — workable, but strictly more fragile than keeping the existing nginx
reverse-proxy topology, which makes the SPA and API the same origin from the browser's perspective. That's why
this runbook deploys **both** the API and the web image as Container Apps rather than putting the SPA on Static
Web Apps.

| Concern | Azure resource | Provisioned by |
|---|---|---|
| API compute | Container Apps (`auditx-api`, internal ingress) | `infra/azure/main.bicep` |
| SPA + reverse proxy | Container Apps (`auditx-web`, external ingress) | `infra/azure/main.bicep` |
| Images | Azure Container Registry | `infra/azure/main.bicep` + CD pipeline |
| Database | Azure SQL (General Purpose Serverless, no auto-pause) | `infra/azure/main.bicep` |
| Cache / session denylist | Azure Cache for Redis (Standard C1, TLS) | `infra/azure/main.bicep` |
| Secrets | Key Vault (RBAC), referenced natively by the Container App | `infra/azure/main.bicep` |
| Evidence + DataProtection key ring | Storage Account, 2 Azure Files shares mounted as Container Apps volumes | `infra/azure/main.bicep` |
| Identity | User-assigned managed identity (ACR pull + Key Vault Secrets User) | `infra/azure/main.bicep` |
| Logs | Log Analytics workspace | `infra/azure/main.bicep` |
| AD reachability | VPN/ExpressRoute to the bank's domain controllers | landing zone (not in this template) |

---

## 2. One-time setup

### 2.1 Resource group and CLI login
```bash
az login
az group create --name rg-auditx-prod --location eastus2
```

### 2.2 GitHub OIDC federation (for the CD workflow, skip if deploying by hand)
Avoids a stored client secret. Create an app registration and federate it to the repo:
```bash
az ad app create --display-name "auditx-github-deploy" --query appId -o tsv
# note the appId, then:
az ad sp create --id <appId>
az role assignment create --assignee <appId> --role Contributor \
  --scope /subscriptions/<sub-id>/resourceGroups/rg-auditx-prod

az ad app federated-credential create --id <appId> --parameters '{
  "name": "gh-main",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:<org>/auditx:ref:refs/heads/main",
  "audiences": ["api://AzureADTokenExchange"]
}'
```
Add to the repo's GitHub Environment secrets (`staging` and/or `prod` — see [.github/workflows/azure-deploy.yml](../.github/workflows/azure-deploy.yml)):

| Secret | Value |
|---|---|
| `AZURE_CLIENT_ID` | the app registration's appId |
| `AZURE_TENANT_ID` | `az account show --query tenantId -o tsv` |
| `AZURE_SUBSCRIPTION_ID` | `az account show --query id -o tsv` |
| `AZURE_RESOURCE_GROUP` | `rg-auditx-prod` |
| `ACR_NAME` | chosen after first deploy (bicep output `acrLoginServer`, minus `.azurecr.io`) — or precreate and pass as a bicep param if you want a stable name before first apply |
| `SQL_ADMIN_PASSWORD` | generate with a password manager; ≥ 16 chars, mixed classes |
| `JWT_SIGNING_KEY` | `openssl rand -base64 48` — ≥ 32 bytes, the startup guard rejects anything shorter or the committed placeholder |
| `AD_SERVICE_ACCOUNT_PASSWORD` | the LDAPS bind account's password (or use `AD_API_KEY` if `identityProvider=ActiveDirectoryApi`) |

Also set a repo/environment **variable** `WEB_FQDN` once you know it (see §3 step 4) so `corsOrigin` and the smoke
test resolve correctly on subsequent deploys.

For prod, add a **required reviewer** on the `prod` GitHub Environment (Settings → Environments) so
`workflow_dispatch` pauses for manual approval before `provision` runs.

> Deploying by hand instead of via the pipeline? Skip this section — just `az login` and follow §3 with your own
> `az acr build` / `docker push` in place of the `build-and-push` job.

---

## 3. Deploy

### Option A — GitHub Actions (recommended)
1. Push the secrets/variables from §2.2.
2. Actions tab → **Azure Deploy** → **Run workflow** → choose `staging` or `prod`.
3. This runs, in order: build+push both images to ACR → generate (not apply) the idempotent EF migration script
   as a downloadable artifact → `bicep what-if` then `bicep deploy` → a smoke test against `/admin/health`.
4. After the **first** deploy, read the `webFqdn` output (Azure Portal → the deployment → Outputs, or
   `az deployment group show -g rg-auditx-prod -n main --query properties.outputs`), set it as the `WEB_FQDN`
   repo/environment variable, and re-run the `provision` job once so `Cors__Origins__0` is populated (first deploy
   necessarily doesn't know its own hostname yet — a bootstrap chicken-and-egg, resolved by this one extra pass).
5. Apply the migration script (§4) — the pipeline deliberately does not do this automatically.

### Option B — manual CLI
```bash
# 1. Build + push images (skip if reusing images already in ACR)
az acr create --resource-group rg-auditx-prod --name auditxacr<suffix> --sku Standard --admin-enabled false
az acr build --registry auditxacr<suffix> --image auditx-api:<tag> --file docker/Api.Dockerfile .
az acr build --registry auditxacr<suffix> --image auditx-web:<tag> --file docker/Web.Dockerfile .

# 2. Deploy infrastructure + Container Apps
az deployment group create \
  --resource-group rg-auditx-prod \
  --template-file infra/azure/main.bicep \
  --parameters infra/azure/main.parameters.json \
  --parameters imageTag=<tag> \
  --parameters sqlAdminPassword="$SQL_ADMIN_PASSWORD" \
  --parameters jwtSigningKey="$JWT_SIGNING_KEY" \
  --parameters activeDirectoryServiceAccountPassword="$AD_PASSWORD" \
  --parameters activeDirectoryHost=dc01.bank.local \
  --parameters activeDirectoryBaseDn="DC=bank,DC=local" \
  --parameters activeDirectoryServiceAccountDn="CN=svc-auditx,OU=Service Accounts,DC=bank,DC=local" \
  --parameters activeDirectoryUpnSuffix=bank.local

# 3. Read the SPA hostname and re-deploy once with corsOrigin set (see step 4 above)
az deployment group show -g rg-auditx-prod -n main --query properties.outputs.webFqdn.value -o tsv
az deployment group create ... --parameters corsOrigin=https://<webFqdn>   # repeat the same command with this added
```

`infra/azure/main.bicep` provisions everything in the table in §1. Review its header comment before running against
a real bank subscription — it deliberately uses public endpoints + firewall allow-lists as a reference topology;
see §7 for the private-endpoint hardening path.

---

## 4. Database migrations (controlled — same policy as the main runbook §4 option B)

`Database__MigrateOnStartup` is `false` in this template (see the `apiContainerApp` env vars in the bicep). Apply
migrations as a reviewed, out-of-band step, never automatically on container start, for the same reason the main
runbook gives: a bank's DBA reviews and runs it, on their schedule, against a fresh backup.

```bash
# Generates ./auditx-migrations-<tag>.sql — the CD pipeline does this for you as a build artifact.
cd backend
dotnet ef migrations script --idempotent \
  --project src/AuditX.Infrastructure --startup-project src/AuditX.Api \
  -o ../auditx-migrations.sql
```
Run the script from wherever the DBA has connectivity to Azure SQL — Azure Data Studio, `sqlcmd`, or (if the
runner needs direct access) a **temporary, scoped** SQL firewall rule for that one IP, removed immediately after:
```bash
MY_IP=$(curl -s ifconfig.me)
az sql server firewall-rule create -g rg-auditx-prod -s <sql-server-name> -n temp-migrate --start-ip-address "$MY_IP" --end-ip-address "$MY_IP"
sqlcmd -S <sql-server-name>.database.windows.net -d auditx -U auditxadmin -P "$SQL_ADMIN_PASSWORD" -i auditx-migrations.sql
az sql server firewall-rule delete -g rg-auditx-prod -s <sql-server-name> -n temp-migrate
```
The seed (`DbSeeder`) still runs on API startup regardless and is idempotent (built-in roles/permissions, default
dashboards, notification templates, AC/CIA roles, grid/report defaults) — only the schema migration is gated.

---

## 5. Configuration reference (Azure-specific)

All the config keys in [DEPLOYMENT_RUNBOOK.md §0](DEPLOYMENT_RUNBOOK.md#0-architecture--runtime-dependencies)
apply; this table only covers where each one comes from **in this topology**.

| Key | Source in this template |
|---|---|
| `ConnectionStrings__Default` | Key Vault secret `sql-connection-string`, referenced by the Container App via managed identity (never touches a plain env var) |
| `Jwt__SigningKey` | Key Vault secret `jwt-signing-key` |
| `Redis__ConnectionString` | Key Vault secret `redis-connection-string` (host:6380, TLS, access key) |
| `Identity__Provider` + `ActiveDirectory__*` | plain Container App env vars, except `ServiceAccountPassword` (Key Vault secret `ad-service-account-password`) |
| `Cors__Origins__0` | the web Container App's own FQDN — same-origin, so this is really a defense-in-depth setting, not load-bearing for the primary flow |
| `Storage__EvidenceRoot` | `/mnt/evidence`, an Azure Files share (`auditx-evidence`) mounted as a Container Apps volume |
| `DataProtection__KeyRingPath` | `/mnt/keyring`, a **separate** Azure Files share (`auditx-keyring`) — kept apart from evidence so a future evidence-retention purge job can never touch the key ring |
| `Database__MigrateOnStartup` | `false` — see §4 |

Rotating a secret: update the Key Vault secret value, then issue a new Container Apps revision (`az containerapp
revision restart` or redeploy) — Container Apps resolves `keyVaultUrl` secrets at revision start, not per-request,
so existing revisions keep the old value until restarted.

---

## 6. DNS, TLS, and going through Front Door / App Gateway

The reference template terminates TLS at the Container Apps built-in ingress (`*.azurecontainerapps.io`, a
managed certificate). For a bank-facing production hostname:
1. Add a custom domain + managed certificate to the `auditx-web` Container App:
   ```bash
   az containerapp hostname add --hostname auditx.bank.com --resource-group rg-auditx-prod --name auditx-web
   az containerapp hostname bind --hostname auditx.bank.com --resource-group rg-auditx-prod --name auditx-web --environment <cae-name> --validation-method CNAME
   ```
2. Or front it with **Azure Front Door / Application Gateway + WAF** per the bank's landing-zone standard, pointed
   at the Container App's default FQDN as origin, with the bank's TLS certificate and WAF policy. Preferred once
   the bank requires a WAF in front of anything internet-facing.

Either way, keep the SPA→API path same-origin (through nginx) — don't put Front Door in front of the API alone
with a different hostname than the SPA, or you reintroduce the cross-site-cookie problem this topology avoids.

---

## 7. Landing-zone hardening (do before real bank data goes through this)

The reference `main.bicep` is deliberately readable over a straightforward public-endpoint-plus-firewall topology.
Before production use with real bank data, adapt to the bank's landing zone:

- **VNet-integrate the Container Apps environment** (`vnetConfiguration` on `managedEnvironments`) and put
  `internal: true` on the environment so `auditx-web`'s external ingress is only reachable via an internal load
  balancer / Application Gateway, not directly from the internet.
- **Private endpoints** for Azure SQL, Azure Cache for Redis, the Storage Account, and Key Vault; set
  `publicNetworkAccess: 'Disabled'` on each once the private endpoints are live (the template currently sets
  `'Enabled'` with an `AllowAzureServices` firewall rule as the reference default).
- **VPN/ExpressRoute** from the VNet to the bank's on-prem Active Directory domain controllers for LDAPS — this
  template cannot provision that side; coordinate with network/landing-zone owners.
- Consider **Azure SQL Managed Instance** instead of Azure SQL Database if the bank needs full SQL Agent / cross-
  database query parity with the on-prem topology.
- Turn on **Microsoft Defender for Cloud** plans for Container Apps, SQL, and Key Vault.
- Point Container Apps' Log Analytics workspace (already provisioned) at the bank's central Sentinel/SIEM workspace,
  or configure a Log Analytics cross-workspace query / export — this is the Azure-side half of the M14 SIEM export
  seam mentioned in the main runbook.

---

## 8. Post-deploy smoke test

Same checks as [DEPLOYMENT_RUNBOOK.md §5](DEPLOYMENT_RUNBOOK.md#5-post-deploy-smoke-test-all-environments), run
against the Container Apps FQDN (everything goes through the `web` app — same-origin):
```bash
WEB=https://<webFqdn or custom domain>

curl -fsS "$WEB/admin/health"                                    # {"status":"healthy","checks":{"database":true,"redis":true},...}
curl -fsS "$WEB/swagger/v1/swagger.json" > /dev/null && echo ok   # OpenAPI doc generates
curl -s -o /dev/null -w "%{http_code}\n" "$WEB/api/v1/audits"     # 401 unauthenticated
curl -sI "$WEB/" | grep -iE "x-content-type-options|x-frame-options"
```
Then sign in as an AD-mapped Administrator through the browser and run the functional acceptance list in the main
runbook §5 (create an audit, raise an exception/MAP, generate a report, generate an AC pack).

---

## 9. Scaling & cost notes

- **API**: `apiMinReplicas=2` (bicep param) keeps the DataProtection key ring and Redis denylist meaningfully
  shared across instances from the start — both already work multi-instance because the key ring lives on a
  shared Azure Files mount and Hangfire coordinates recurring jobs through SQL Server (see the main runbook's
  scale-out note). HTTP-concurrency-based autoscale (`concurrentRequests: 50`) covers burst traffic up to
  `apiMaxReplicas`.
- **Database**: `GP_S_Gen5_2` (General Purpose Serverless, 2 vCore ceiling, auto-pause disabled) is a reasonable
  starting point for a mid-size bank's internal-audit workload — resize (`az sql db update --edition ... --family
  ... --capacity ...`) once you have real usage data. Auto-pause is intentionally off: a bank workload shouldn't
  eat a cold-start on the first request of the day.
- **Redis**: Standard C1 gives HA (primary/replica) — don't drop to Basic in production; a Redis outage degrades
  every authenticated request (permission cache + session denylist), per the main runbook's operational notes.
- **RabbitMQ**: the local Compose stack runs a RabbitMQ container and `AuditX.Infrastructure` references
  `MassTransit.RabbitMQ`, but nothing in `appsettings.json` wires up a broker connection today — this template
  does **not** provision Azure Service Bus. Add it only once a feature actually depends on message-broker
  connectivity; provisioning it speculatively would be unused cost.

---

## 10. Rollback

Same policy as the main runbook §6, Azure-specific mechanics:
- **App**: redeploy the previous `imageTag` — `az containerapp update --name auditx-api --resource-group
  rg-auditx-prod --image <acr>/auditx-api:<previous-tag>` (and the same for `auditx-web`). Container Apps keeps
  prior revisions; you can also `az containerapp revision activate`/`deactivate` to flip traffic without a rebuild
  if the previous revision is still provisioned.
- **Database**: migrations are forward-only by policy; restore from the pre-migration Azure SQL automated backup
  (`az sql db restore`) or point-in-time restore if a bad migration needs undoing.
- **Secrets**: Key Vault has soft-delete + purge protection enabled (`softDeleteRetentionInDays: 90` in the
  template) — a deleted secret is recoverable within that window.
- **DataProtection key ring**: never delete the `auditx-keyring` file share — losing it makes stored M14
  integration credentials undecryptable. It isn't touched by any migration or app-update path in this topology.

---

## 11. Troubleshooting

| Symptom | Likely cause |
|---|---|
| `502` from the web app on `/api/*` | `API_UPSTREAM` env var missing/wrong on `auditx-web`, or `auditx-api` has no healthy revision — check `az containerapp revision list --name auditx-api` |
| API container app fails to start, logs show the startup safety guard rejecting boot | A Key Vault-backed secret didn't resolve (check the Container App's managed identity has `Key Vault Secrets User` — the `keyVaultRoleAssignment` in the bicep) or `Jwt__SigningKey`/`ConnectionStrings__Default` came through empty |
| `401`/timeout on every login attempt | VPN/ExpressRoute to the domain controller isn't up, or `ActiveDirectory__Host`/`BaseDn`/`ServiceAccountDn` misconfigured — this template cannot provision the network path, only the app config |
| Secret rotation doesn't seem to take effect | Container Apps resolves Key Vault secrets at revision start, not live — restart/redeploy the revision after rotating |
| `az deployment group create` fails on the Key Vault role assignment | RBAC propagation can lag a minute or two after Key Vault creation; re-run the deployment |
