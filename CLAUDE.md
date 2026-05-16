# UpdateHub — Claude Code Context

## What this is

Self-hosted update server for personal desktop applications (Tauri, Avalonia/.NET, etc.).
Clean Architecture, .NET 10, Blazor Server, SQLite, local file storage.

## Architecture: 4 projects

| Project | Responsibility | Dependencies |
|---|---|---|
| `UpdateHub.Domain` | Entities + enums | none |
| `UpdateHub.Application` | Services + interfaces + DTOs | Domain |
| `UpdateHub.Infrastructure` | EF Core, repositories, file storage | Application + Domain |
| `UpdateHub.Web` | Blazor UI + Minimal API + DI bootstrap | Application + Infrastructure |

**Dependency rule**: inner layers never reference outer layers.

## Key files

| File | Purpose |
|---|---|
| `src/UpdateHub.Infrastructure/DependencyInjection.cs` | Registers all services — the only place with concrete types |
| `src/UpdateHub.Application/Interfaces/` | Contracts between Application and Infrastructure |
| `src/UpdateHub.Application/Services/AdminService.cs` | All admin CRUD, uses repository interfaces |
| `src/UpdateHub.Application/Services/UpdateResolverService.cs` | Update check + Tauri manifest logic |
| `src/UpdateHub.Infrastructure/Persistence/AppDbContext.cs` | EF Core with SQLite, `EnsureCreated` |
| `src/UpdateHub.Web/Program.cs` | Bootstrap: calls `AddInfrastructure()`, registers auth |
| `src/UpdateHub.Web/Endpoints/` | Minimal API — public + CI |
| `src/UpdateHub.Web/Components/Pages/` | Blazor admin pages |

## Public API

```
GET  /api/apps/{slug}/update?version=X&platform=windows&arch=x64
GET  /api/apps/{slug}/tauri/latest.json
GET  /api/downloads/{artifactId}
POST /api/ci/apps/{slug}/releases   (header: X-UpdateHub-Token)
```

## Running locally

```bash
cd src/UpdateHub.Web
dotnet run
# http://localhost:5000 — admin / admin123
```

Dev hash in `appsettings.Development.json` is for password `admin123`.

## Config keys

- `UpdateHub:BaseUrl` — public URL (used in download links)
- `UpdateHub:DatabasePath` — SQLite file path
- `UpdateHub:StoragePath` — artifact directory
- `UpdateHub:CiToken` — CI upload secret
- `UpdateHub:Admin:Username` / `UpdateHub:Admin:PasswordHash` — bcrypt hash

## Adding new storage backend

1. Implement `IArtifactStorage` (Application layer interface)
2. Register in `DependencyInjection.cs` instead of `LocalArtifactStorage`
3. Nothing else needs changing — services depend on the interface

## DB schema changes

Uses `EnsureCreated()` — schema is created once. To change schema:
delete the `.db` file and restart. Add EF Core migrations if you need upgrade paths.

## Conventions

- Blazor pages `@rendermode InteractiveServer`
- Login uses `EmptyLayout` (no sidebar)
- Blazor pages inject Application services only — never Infrastructure types
- No comments unless WHY is non-obvious
- No `ArtifactStorageService` in Blazor — `AdminService` handles storage internally
