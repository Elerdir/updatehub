# UpdateHub — Claude Code Context

## Project

Self-hosted update server for personal desktop applications (Tauri, Avalonia/.NET, etc.).
Simple, no multi-tenant, no licensing — just apps, releases and artifacts.

## Stack

- **Backend + UI**: ASP.NET Core 10, Blazor Server
- **Database**: SQLite via EF Core 9 (`EnsureCreated` — no migrations)
- **Auth**: Cookie auth, single admin user, bcrypt password in config
- **Storage**: Local filesystem, SHA-256 computed on upload

## Key files

| File | Purpose |
|---|---|
| `src/UpdateHub.Web/Program.cs` | Bootstrap, DI, auth endpoints |
| `src/UpdateHub.Web/Data/AppDbContext.cs` | EF Core context |
| `src/UpdateHub.Web/Services/UpdateResolverService.cs` | Update check logic |
| `src/UpdateHub.Web/Services/AdminService.cs` | All CRUD for the UI |
| `src/UpdateHub.Web/Endpoints/PublicEndpoints.cs` | Public API (no auth) |
| `src/UpdateHub.Web/Endpoints/CiEndpoints.cs` | CI upload endpoint |
| `src/UpdateHub.Web/Components/Pages/` | Blazor pages |

## Public API

```
GET  /api/apps/{slug}/update?version=X&platform=windows&arch=x64
GET  /api/apps/{slug}/tauri/latest.json
GET  /api/downloads/{artifactId}
POST /api/ci/apps/{slug}/releases   (X-UpdateHub-Token header)
```

## Running locally

```bash
cd src/UpdateHub.Web
dotnet run
# http://localhost:5000 — admin/admin123 (from appsettings.Development.json)
```

## Config

Everything via `appsettings.json` or environment variables:
- `UpdateHub:BaseUrl` — public URL (used in download links)
- `UpdateHub:CiToken` — CI upload secret
- `UpdateHub:Admin:Username` / `UpdateHub:Admin:PasswordHash`

## Dev password hash

`appsettings.Development.json` has a pre-baked hash for `admin123`.
To generate a new hash: use https://bcrypt.online/ with cost 12.

## DB schema changes

Schema changes require deleting the `.db` file and restarting — `EnsureCreated` won't migrate.
This is intentional for v1 simplicity. Add EF Core migrations if you need upgrades.

## Conventions

- Services are scoped (per Blazor circuit)
- `ArtifactStorageService` is singleton (stateless file ops)
- Blazor pages use `@rendermode InteractiveServer`
- Login page uses `EmptyLayout` (no sidebar)
- No comments in code unless the WHY is non-obvious
