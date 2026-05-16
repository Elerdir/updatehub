# ── Build stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first so Docker can cache the restore layer
COPY global.json .
COPY src/UpdateHub.Domain/UpdateHub.Domain.csproj             src/UpdateHub.Domain/
COPY src/UpdateHub.Application/UpdateHub.Application.csproj   src/UpdateHub.Application/
COPY src/UpdateHub.Infrastructure/UpdateHub.Infrastructure.csproj src/UpdateHub.Infrastructure/
COPY src/UpdateHub.Web/UpdateHub.Web.csproj                   src/UpdateHub.Web/

RUN dotnet restore src/UpdateHub.Web/UpdateHub.Web.csproj

# Copy source and publish.
# We intentionally let publish re-run restore on its own. With --no-restore,
# the .NET 10 static-web-assets target that materialises framework files
# (notably /_framework/blazor.web.js) gets skipped, the published wwwroot
# ends up without an _framework directory, and the Blazor circuit can't
# hydrate in the browser. Restore is a few extra seconds, well worth it.
COPY src/ src/
RUN dotnet publish src/UpdateHub.Web/UpdateHub.Web.csproj \
        -c Release \
        -o /app/publish

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl is needed for the HEALTHCHECK
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Persistent data directories (override with -v / volumes in docker-compose)
RUN mkdir -p /app/data/artifacts /app/logs

ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    ASPNETCORE_URLS=http://+:8080 \
    UpdateHub__DatabasePath=/app/data/updatehub.db \
    UpdateHub__StoragePath=/app/data/artifacts

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
    CMD curl -fsS http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "UpdateHub.Web.dll"]
