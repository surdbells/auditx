# syntax=docker/dockerfile:1
# Multi-stage build for the AuditX Angular SPA, served by Nginx.

FROM node:24-alpine AS build
WORKDIR /src
COPY frontend/auditx-web/package*.json ./
RUN npm ci
COPY frontend/auditx-web/ ./
RUN npm run build -- --configuration production

FROM nginx:1.27-alpine AS runtime
# Templated (not static) so the API upstream is substituted at container start via nginx's built-in
# docker-entrypoint envsubst step. Default matches the Compose/on-prem service name; Azure Container Apps
# overrides API_UPSTREAM at deploy time (see docs/AZURE_DEPLOYMENT_RUNBOOK.md).
COPY docker/nginx.conf.template /etc/nginx/templates/default.conf.template
ENV API_UPSTREAM=api:8080
COPY --from=build /src/dist/auditx-web/browser /usr/share/nginx/html
EXPOSE 80
HEALTHCHECK CMD wget -qO- http://localhost/ || exit 1
