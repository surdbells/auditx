#!/usr/bin/env bash
# ---------------------------------------------------------------------------------------------------------------------
# Runs ON the aaPanel Linux server, piped in over SSH by the "Deploy (aaPanel)" GitHub Actions workflow
# (.github/workflows/deploy-aapanel.yml). It updates the server's git checkout to the requested ref and
# (re)builds the demo/UAT Docker stack. See docs/AAPANEL_DEPLOYMENT.md (§ CI/CD).
#
# Usage (the workflow does this for you):   bash -s -- <deploy-dir> <git-ref>
#   $1  deploy-dir : the git checkout of this repo on the server (e.g. /opt/auditx)
#   $2  git-ref    : branch, tag, or SHA to deploy (e.g. main)
#
# The server is treated as a pure mirror of the remote: tracked files are hard-reset to the target commit.
# Untracked, gitignored state (.env, Docker named volumes) is preserved.
# ---------------------------------------------------------------------------------------------------------------------
set -euo pipefail

DEPLOY_DIR="${1:?deploy dir required (arg 1)}"
REF="${2:-main}"
COMPOSE_FILE="docker-compose.aapanel.yml"

echo "==> AuditX deploy — dir=${DEPLOY_DIR} ref=${REF}"
cd "$DEPLOY_DIR"

# --- Update the working tree to the requested commit (branch/tag/SHA), discarding tracked-file changes ---
git fetch --all --prune --tags
TARGET="$(git rev-parse --verify --quiet "origin/${REF}^{commit}" || git rev-parse --verify --quiet "${REF}^{commit}" || true)"
if [ -z "$TARGET" ]; then
  echo "ERROR: ref '${REF}' not found on origin or locally." >&2
  exit 1
fi
git checkout -f "$TARGET"
echo "==> Now at $(git rev-parse --short HEAD): $(git log -1 --pretty=%s)"

# --- Preconditions: secrets must already exist on the server (first-time setup creates .env) ---
if [ ! -f .env ]; then
  echo "ERROR: .env not found in ${DEPLOY_DIR}. Create it from .env.aapanel.example before deploying." >&2
  exit 1
fi
if [ ! -f "$COMPOSE_FILE" ]; then
  echo "ERROR: ${COMPOSE_FILE} not found in ${DEPLOY_DIR}." >&2
  exit 1
fi

# --- Build + (re)start the stack ---
echo "==> docker compose up -d --build"
docker compose -f "$COMPOSE_FILE" up -d --build --remove-orphans
docker compose -f "$COMPOSE_FILE" ps
docker image prune -f >/dev/null 2>&1 || true

# --- Best-effort health probe against the web container on its loopback port (from .env; default 8090) ---
WEB_PORT="$(sed -n 's/^WEB_PORT=//p' .env | tr -d '"'\'' ' | head -n1)"
WEB_PORT="${WEB_PORT:-8090}"
echo "==> Waiting for the web container on 127.0.0.1:${WEB_PORT} (first boot runs migrations + seed)…"
ok=0
for _ in $(seq 1 40); do
  code="$(curl -s -o /dev/null -w '%{http_code}' "http://127.0.0.1:${WEB_PORT}/" || true)"
  if [ "$code" = "200" ]; then echo "==> web up (HTTP 200)"; ok=1; break; fi
  sleep 3
done
if [ "$ok" != "1" ]; then
  echo "WARN: web not returning 200 yet after ~2 min. It may still be seeding — check: docker compose -f ${COMPOSE_FILE} logs -f api" >&2
fi
echo "==> Deploy complete."
