# AuditX — aaPanel (Linux) demo/UAT deployment

How to stand up AuditX on a Linux server managed by **aaPanel**, reachable over the internet, for **demo / UAT**.

> **What this is.** A showcase/testing deployment that uses the **seeded logins** (`admin` / `manager` / `auditor` /
> `auditee`, password `Passw0rd!`) so you can sign in without an Active Directory. To allow those seeded accounts the
> API runs in the **Development** ASP.NET environment.
>
> **What this is _not_.** It is **not production** and must not hold real or confidential data. Anyone who can reach it
> can sign in as the administrator, so it **must** sit behind the aaPanel reverse proxy with **TLS** and an **access
> gate** (Basic Auth or an IP allow-list) — both covered below. When you are ready for real users, switch to LDAP/AD
> auth and the Production environment per [DEPLOYMENT_RUNBOOK.md](DEPLOYMENT_RUNBOOK.md).

The stack is five Docker containers: **SQL Server**, **Redis**, **RabbitMQ**, the **API**, and the **web** container
(Nginx that serves the Angular app *and* reverse-proxies `/api` to the API — so everything is same-origin behind one
port). aaPanel only needs to be a TLS reverse proxy in front of the web container.

```
Browser ──HTTPS──▶ aaPanel Nginx (TLS + access gate) ──▶ 127.0.0.1:8090 (web container)
                                                             ├── serves the Angular SPA
                                                             └── proxies /api ─▶ api:8080 ─▶ SQL Server / Redis
```

---

## 0. Prerequisites

- A Linux server (Ubuntu 22.04/24.04 or Debian 12 recommended) with **aaPanel** installed.
- **≥ 4 GB RAM** and ~15 GB free disk. SQL Server alone wants ~2 GB; the whole stack sits around 2.5–3 GB.
- The **Docker Manager** aaPanel plugin (App Store → Docker Manager → Install). This installs Docker Engine + Compose.
- Your cloud firewall/security group and aaPanel's **Security** panel allowing inbound on the port you'll serve
  (443 if you use the reverse proxy — recommended; or `8090` if you go direct).
- The code on the server (next step).

---

## 1. Get the code onto the server

Use aaPanel **Terminal** (or SSH). Clone into a path outside any web root, e.g. `/opt`:

```bash
mkdir -p /opt && cd /opt
git clone <your-repo-url> auditx
cd auditx
```

(No repo remote? Zip the project, upload via aaPanel **Files**, and unzip into `/opt/auditx`.)

> **Prefer to stay in the aaPanel UI?** Steps 1–3 use the terminal because it's the most reliable, but you can do the
> whole thing without it — see [**Alternative: deploy entirely in the aaPanel UI**](#alternative-deploy-entirely-in-the-aapanel-ui-no-terminal) below, then rejoin at §4.

---

## 2. Configure secrets

Create the `.env` the compose file reads, from the template:

```bash
cd /opt/auditx
cp .env.aapanel.example .env
```

Generate strong secrets and edit `.env`:

```bash
openssl rand -base64 24   # use for SA_PASSWORD  (SQL Server needs 8+ chars, mixed classes)
openssl rand -base64 48   # use for JWT_SIGNING_KEY
nano .env
```

Set at least:

| Key | Value |
|---|---|
| `SA_PASSWORD` | the first generated secret (16+ chars) |
| `JWT_SIGNING_KEY` | the second generated secret |
| `PUBLIC_ORIGIN` | the exact URL users will open — e.g. `https://203.0.113.10` (proxy + self-signed TLS) |
| `WEB_PORT` | host loopback port for the web container (default `8090`) |

`PUBLIC_ORIGIN` must match what the browser actually shows (scheme, host, and port). It drives CORS and the links in
notification emails. If you go the direct route (§4B) it would be `http://<server-ip>:8090`.

---

## 3. Bring up the stack

From `/opt/auditx`:

```bash
docker compose -f docker-compose.aapanel.yml up -d --build
```

First run builds the API and web images and pulls SQL Server/Redis/RabbitMQ — allow a few minutes. Watch the API apply
migrations and seed the demo data:

```bash
docker compose -f docker-compose.aapanel.yml ps            # sqlserver/redis healthy, api + web up
docker compose -f docker-compose.aapanel.yml logs -f api   # wait for "Application started"; Ctrl-C to stop tailing
```

> You can also do this from **Docker Manager → Compose** in the aaPanel UI: point it at
> `/opt/auditx/docker-compose.aapanel.yml`. The CLI above is the most reliable.

Quick local check on the server (before wiring the proxy):

```bash
curl -s -o /dev/null -w "%{http_code}\n" http://127.0.0.1:8090/          # 200 — SPA served
curl -s http://127.0.0.1:8090/api/v1/../../swagger/v1/swagger.json | head -c 40   # OpenAPI via the web proxy
```

---

## Alternative: deploy entirely in the aaPanel UI (no terminal)

This replaces **§§1–3** with point-and-click steps in aaPanel. When you're done here, continue at **§4**. (§4 onward
is already a UI flow.)

### A1. Get the code up with **Files**
1. On your PC, **zip the project folder** (the repo). You can safely leave out `node_modules`, `bin`, `obj`, `dist`,
   `.angular`, and `.git` — the Docker build regenerates what it needs (they're in `.dockerignore`), and the zip stays
   small.
2. aaPanel → **Files**. Go to `/opt` (use **Create → Directory** if it doesn't exist).
3. **Upload** the zip into `/opt`, then select it → **Unzip**. You should end up with `/opt/auditx` containing
   `docker-compose.aapanel.yml`.

### A2. Create `.env` with the Files editor
1. In `/opt/auditx`, select **`.env.aapanel.example`** → **Copy**, then rename the copy to **`.env`** (or **Create → File**
   named `.env` and paste the template's contents).
2. Generate two secrets — no terminal needed: use aaPanel's built-in **random-password generator** (the dice/refresh
   icon on any password field, e.g. in **Databases → Add**), or a password manager. Any 16+ character strong string
   works for `SA_PASSWORD`; any 32+ character random string for `JWT_SIGNING_KEY`.
3. Double-click **`.env`** to edit it and set `SA_PASSWORD`, `JWT_SIGNING_KEY`, `PUBLIC_ORIGIN`
   (your `https://<server-ip>`), and `WEB_PORT` (default `8090`). Save.

### A3. Run it with **Docker Manager → Compose**
1. aaPanel → **Docker** → **Compose** (labelled *Compose* / *Orchestration* / *Project* depending on your version) →
   **Add / Create**.
2. Choose **use an existing compose file** and point it at `/opt/auditx/docker-compose.aapanel.yml`; name the project
   `auditx-demo`. If the UI has an **Environment file** field, set it to `/opt/auditx/.env` (otherwise it picks up the
   `.env` sitting next to the compose file automatically).
3. **Deploy / Up.** The first run builds the API and web images — allow a few minutes.
4. Watch progress: Docker Manager lists the five containers; open the **auditx-api** container → **Logs** and wait for
   `Application started` (migrations + demo seed run first).

> **If your Docker Manager's Compose UI can't build images** (older builds only *run* prebuilt images): either run the
> one command `docker compose -f docker-compose.aapanel.yml up -d --build` once from **Files → Terminal**, or ask for
> the **prebuilt-image** compose variant — then the server only ever pulls ready-made images and never builds.

**Day-2 in the UI:** Docker Manager gives you **Start / Stop / Restart** per container or for the whole `auditx-demo`
project, and **Logs** per container. To update after code changes, re-upload the zip (or `git pull`) and hit
**Rebuild / Up** on the project. The reset-to-fresh-DB action is **Down** with "remove volumes" ticked.

Now continue at **§4** to put aaPanel's reverse proxy, TLS, and the access gate in front.

---

## 4. (Recommended) Put aaPanel in front — TLS + access gate

Because this is Development mode with seeded accounts, **do not** expose it ungated. Front it with aaPanel Nginx.

### 4A. Reverse proxy with a self-signed cert (no domain yet)

1. **Website → Add site.** For the domain, enter your **server IP** (aaPanel accepts an IP here). No PHP/database
   needed — it's a static/proxy site.
2. Open the new site → **Reverse proxy → Add reverse proxy:**
   - Target URL: `http://127.0.0.1:8090`
   - Send domain (Host): leave as `$host`.
   - Enable **WebSocket** support (harmless; fine to leave on).
3. Open the site → **SSL → Self-signed certificate → Apply**, then toggle **Force HTTPS** on. (Browsers will show a
   "not private" warning for a self-signed cert on an IP — expected for a demo. Later, point a domain at the server and
   switch this to **Let's Encrypt** for a trusted cert.)
4. **Raise the upload limit** (evidence files can be large). Site → **Config** (Nginx conf) and set inside `server { }`:
   ```nginx
   client_max_body_size 0;      # or e.g. 2048m
   ```
   Save and **reload** Nginx.
5. **Gate access.** Site → **Password access (Basic Auth)** → set a username/password. Everyone must enter it before
   reaching the app. (Alternatively, restrict by source IP in **Security**, or add `allow <your-ip>; deny all;` to the
   site's Nginx config.)

Set `PUBLIC_ORIGIN=https://<server-ip>` in `.env` to match, then recreate the API so CORS/links use it:

```bash
docker compose -f docker-compose.aapanel.yml up -d api
```

### 4B. Quick alternative — direct, no proxy (ungated)

Only for a throwaway test on a trusted network. Edit `docker-compose.aapanel.yml`, change the web port mapping to
`"${WEB_PORT}:80"` (drop the `127.0.0.1:` prefix), open `8090` in the firewall, set `PUBLIC_ORIGIN=http://<server-ip>:8090`,
and `docker compose -f docker-compose.aapanel.yml up -d web api`. Anyone who finds the IP can sign in as admin — prefer 4A.

---

## 5. Smoke test

From your laptop (accept the self-signed warning if prompted), or on the server against the proxy:

```bash
BASE=https://<server-ip>            # your PUBLIC_ORIGIN
curl -sk -c jar.txt -X POST $BASE/api/v1/auth/login \
  -H "Content-Type: application/json" -d '{"username":"admin","password":"Passw0rd!"}'   # Set-Cookie: auditx.session
curl -sk -b jar.txt $BASE/api/v1/users/me                                                # 200 + admin profile
```

Then open `PUBLIC_ORIGIN` in a browser and sign in as **`admin` / `Passw0rd!`**. Seeded users:

| Username | Role | Password |
|---|---|---|
| `admin` | AuditX Administrator | `Passw0rd!` |
| `manager` / `auditor` / `auditee` | as configured in the demo seed | `Passw0rd!` |

---

## 6. Day-2 operations

```bash
cd /opt/auditx
# Update to newer code:
git pull
docker compose -f docker-compose.aapanel.yml up -d --build
# Logs / status:
docker compose -f docker-compose.aapanel.yml logs -f api
docker compose -f docker-compose.aapanel.yml ps
# Stop (keep data):        docker compose -f docker-compose.aapanel.yml down
# Reset to a fresh DB:     docker compose -f docker-compose.aapanel.yml down -v   (drops SQL/Redis/evidence volumes)
```

Data lives in the named Docker volumes `auditx-demo_sqlserver-data`, `redis-data`, `rabbitmq-data`, `evidence-data`.
Back them up if the demo data matters. RabbitMQ is optional — the API boots without it; remove the service from the
compose to save ~150 MB RAM.

---

## 7. CI/CD — automatic deploys from GitHub

Once the stack has been deployed once by hand (§§1–4), every push to `main` can redeploy it automatically. The
pipeline is [`.github/workflows/deploy-aapanel.yml`](../.github/workflows/deploy-aapanel.yml): after the **CI**
workflow passes on `main`, GitHub Actions SSHes into the server, fast-forwards the `/opt/auditx` checkout, and runs
`docker compose … up -d --build` (remote steps: [`deploy/aapanel-deploy.sh`](../deploy/aapanel-deploy.sh)). You can
also run it by hand from the **Actions** tab.

### 7.1 One-time server setup
1. The stack must already be up from §§1–4, so `/opt/auditx` is a git checkout with a working `.env`.
2. **Let the server pull from GitHub.** For a private repo, add a read-only **deploy key**:
   ```bash
   ssh-keygen -t ed25519 -f ~/.ssh/auditx_repo -N ""      # on the server
   cat ~/.ssh/auditx_repo.pub                              # add in GitHub → repo → Settings → Deploy keys (read-only)
   cd /opt/auditx
   git config core.sshCommand "ssh -i ~/.ssh/auditx_repo -o IdentitiesOnly=yes"
   git remote set-url origin git@github.com:<owner>/<repo>.git
   git fetch                                               # confirm it works
   ```
   (Public repo? Skip this — just make sure `origin` is set.)
3. **Create a non-root deploy user** for Actions to SSH in as, with Docker access:
   ```bash
   useradd -m -s /bin/bash deploy && usermod -aG docker deploy
   chown -R deploy:deploy /opt/auditx        # so it can update the checkout and read .env
   ```
4. **Authorise the CI key.** Generate a dedicated keypair for the GitHub → server hop:
   ```bash
   ssh-keygen -t ed25519 -f auditx_ci -N ""
   mkdir -p /home/deploy/.ssh && cat auditx_ci.pub >> /home/deploy/.ssh/authorized_keys
   chown -R deploy:deploy /home/deploy/.ssh && chmod 700 /home/deploy/.ssh && chmod 600 /home/deploy/.ssh/authorized_keys
   ```
   Keep the **private** key `auditx_ci` for the next step; never commit it.
5. Open the SSH port (22 or your custom port) in aaPanel **Security** and the cloud firewall. Optional hardening:
   restrict the source to GitHub Actions' IP ranges (from `https://api.github.com/meta`) and keep SSH key-only
   (`PasswordAuthentication no`).

### 7.2 GitHub setup
1. Repo → **Settings → Environments → New environment** → **`aapanel-demo`**. (Optionally add a required reviewer so
   deploys wait for approval.)
2. On that environment add **secrets**:

   | Secret | Value |
   |---|---|
   | `AAPANEL_SSH_HOST` | server IP |
   | `AAPANEL_SSH_USER` | `deploy` |
   | `AAPANEL_SSH_KEY` | contents of the **private** key `auditx_ci` |
   | `AAPANEL_SSH_PORT` | *(optional)* SSH port if not 22 |

   If your checkout isn't at `/opt/auditx`, also add a **variable** `AAPANEL_DEPLOY_PATH`.

### 7.3 How it runs
- **Automatic:** push to `main` → CI runs → on success, **Deploy (aaPanel)** updates the server.
- **Manual:** Actions tab → **Deploy (aaPanel)** → **Run workflow** (optionally choose a ref).
- The remote script does: fetch → hard-checkout the target commit → `up -d --build` → image prune → health probe. Your
  `.env` and Docker volumes are never touched (they're untracked/managed).
- **Don't want auto-deploy on every push?** Remove the `workflow_run:` trigger from the workflow to make it
  manual-only (like the Azure pipeline).

> **Build-on-server note.** This rebuilds the images on the VPS, which is CPU/RAM-hungry (Angular + .NET). On a small
> box (~2 GB) it can be slow or run out of memory. If that bites, switch to **prebuilt images**: CI builds and pushes
> the API/web images to a registry (e.g. GHCR) and the server only runs `docker compose pull && up -d`. This repo can
> ship that variant on request.

---

## 8. Troubleshooting

| Symptom | Cause / fix |
|---|---|
| API container exits, logs mention a security guard | You changed the environment away from Development. The seeded provider only runs in Development. Keep `ASPNETCORE_ENVIRONMENT=Development` for the demo, or switch to real LDAP/AD auth (see the production runbook). |
| SQL Server keeps restarting | Not enough RAM (needs ~2 GB) or a weak `SA_PASSWORD` (must meet complexity). Check `docker compose -f docker-compose.aapanel.yml logs sqlserver`. |
| Login returns 200 but the app acts logged-out | `PUBLIC_ORIGIN` doesn't match the browser's URL, so CORS rejects it. Set it to the exact scheme/host/port and recreate the api. |
| Uploads fail with `413 Request Entity Too Large` | Raise `client_max_body_size` in the aaPanel site's Nginx config (§4A step 4) and reload. |
| Browser warns the certificate is not trusted | Expected with a self-signed cert on an IP. Point a domain at the server and switch the site's SSL to Let's Encrypt. |
| Can't reach it at all | Open the port (443 for the proxy, or `WEB_PORT`) in both aaPanel **Security** and your cloud firewall/security group. |

---

## 9. Going to production later

This demo intentionally trades security for convenience (seeded accounts, Development mode). For a real deployment:

1. Provide a real identity source — a domain and Active Directory (LDAPS) or the AD-REST gateway — and set
   `Identity:Provider` accordingly.
2. Run the API in the **Production** environment; the safety guard then enforces a real `Jwt:SigningKey`, a real
   identity provider, and a non-empty connection string.
3. Use a trusted TLS certificate (Let's Encrypt once a domain is in place), controlled migrations
   (`Database:MigrateOnStartup=false`), and **do not** seed demo data.

Full guidance and the on-prem/Azure topologies are in [DEPLOYMENT_RUNBOOK.md](DEPLOYMENT_RUNBOOK.md).
