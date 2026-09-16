#!/usr/bin/env bash
# ---------------------------------------------------------------------------------------------------------------------
# Runs ON the aaPanel server, piped in over SSH by the "Deploy (aaPanel)" workflow's prebuilt-image path. It pulls the
# GHCR images and (re)starts the stack — no git checkout, no build on the box. See docs/AAPANEL_DEPLOYMENT.md (§7).
#
# The workflow first scp's docker-compose.images.yml into <deploy-dir>, then runs:  bash -s -- <deploy-dir> <owner> <tag>
#   $1  deploy-dir : dir holding the server's .env + docker-compose.images.yml (e.g. /opt/auditx)
#   $2  ghcr-owner : GitHub owner/org, lowercase (GHCR namespace)
#   $3  image-tag  : image tag to run (e.g. latest or sha-abc1234)
# ---------------------------------------------------------------------------------------------------------------------
set -euo pipefail

DEPLOY_DIR="${1:?deploy dir required (arg 1)}"
export GHCR_OWNER="${2:?ghcr owner required (arg 2)}"
export IMAGE_TAG="${3:-latest}"
COMPOSE_FILE="docker-compose.images.yml"

echo "==> AuditX image deploy — dir=${DEPLOY_DIR} image=ghcr.io/${GHCR_OWNER}/auditx-{api,web}:${IMAGE_TAG}"
cd "$DEPLOY_DIR"

if [ ! -f .env ]; then
  echo "ERROR: .env not found in ${DEPLOY_DIR}. Create it from .env.aapanel.example before deploying." >&2
  exit 1
fi
if [ ! -f "$COMPOSE_FILE" ]; then
  echo "ERROR: ${COMPOSE_FILE} not found in ${DEPLOY_DIR} (the workflow should have copied it)." >&2
  exit 1
fi

# GHCR_OWNER / IMAGE_TAG are exported above so compose interpolates them; the rest come from .env.
echo "==> docker compose pull"
docker compose -f "$COMPOSE_FILE" pull
echo "==> docker compose up -d"
docker compose -f "$COMPOSE_FILE" up -d --remove-orphans
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
