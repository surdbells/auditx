# syntax=docker/dockerfile:1
# Multi-stage build for the AuditX ASP.NET Core 10 API.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore using central package management (copy manifests first for layer caching).
COPY backend/Directory.Build.props backend/Directory.Packages.props backend/AuditX.slnx backend/
COPY backend/src/AuditX.Domain/AuditX.Domain.csproj backend/src/AuditX.Domain/
COPY backend/src/AuditX.Application/AuditX.Application.csproj backend/src/AuditX.Application/
COPY backend/src/AuditX.Infrastructure/AuditX.Infrastructure.csproj backend/src/AuditX.Infrastructure/
COPY backend/src/AuditX.Api/AuditX.Api.csproj backend/src/AuditX.Api/
RUN dotnet restore backend/src/AuditX.Api/AuditX.Api.csproj

COPY backend/ backend/
RUN dotnet publish backend/src/AuditX.Api/AuditX.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
# The evidence / report / AC-pack artefact store (default <content-root>/evidence-store) must be writable by the
# non-root runtime user. Create + own it while still root, THEN drop privileges. In production point
# Storage:EvidenceRoot at a mounted, backed-up share instead (see the deployment runbook).
RUN mkdir -p /app/evidence-store && chown -R $APP_UID /app/evidence-store
# Run as the non-root user provided by the base image.
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "AuditX.Api.dll"]
