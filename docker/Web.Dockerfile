# syntax=docker/dockerfile:1
# Multi-stage build for the AuditX Angular SPA, served by Nginx.

FROM node:24-alpine AS build
WORKDIR /src
COPY frontend/auditx-web/package*.json ./
RUN npm ci
COPY frontend/auditx-web/ ./
RUN npm run build -- --configuration production

FROM nginx:1.27-alpine AS runtime
COPY docker/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/dist/auditx-web/browser /usr/share/nginx/html
EXPOSE 80
HEALTHCHECK CMD wget -qO- http://localhost/ || exit 1
